﻿using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using JsonWebToken.Internal;

namespace JsonWebToken
{
    /// <summary>
    ///   Represents the structure of a JWT value in a read-only form.
    /// </summary>
    /// <remarks>
    ///   This class utilizes resources from pooled memory to minimize the garbage collector (GC)
    ///   impact in high-usage scenarios. Failure to properly Dispose this object will result in
    ///   the memory not being returned to the pool, which will cause an increase in GC impact across
    ///   various parts of the framework.
    /// </remarks>
    public sealed class JwtDocument2 : IDisposable
    {
        private ReadOnlyMemory<byte> _rawValue;
        private byte[]? _rented;
        private readonly JwtHeader _header;
        private readonly JwtPayloadDocument? _payload;

        public JwtDocument2(JwtHeader header, ReadOnlyMemory<byte> rawValue, byte[] rented)
        {
            Header = header;
            _rawValue = rawValue;
            _rented = rented;
        }

        public JwtDocument2(TokenValidationError error)
        {
            Error = error;
        }

        public JwtDocument2(JwtHeader header, JwtPayloadDocument payload)
        {
            Header = header;
            _payload = payload;
        }

        public JwtDocument2(JwtHeader header, JwtPayloadDocument payload, TokenValidationError error)
        {
            Header = header;
            _payload = payload;
            Error = error;
        }

        public TokenValidationError? Error { get; }
        public JwtHeader? Header { get; }
        public JwtPayloadDocument? Payload => _payload;
        public ReadOnlyMemory<byte> RawValue => _rawValue;

        public static bool TryParse(ReadOnlySpan<byte> utf8Token, TokenValidationPolicy policy, out JwtDocument2 document)
        {
            if (policy is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.policy);
            }

            TokenValidationError? error;
            if (utf8Token.IsEmpty)
            {
                error = TokenValidationError.MalformedToken();
                goto TokenAnalyzed;
            }

            if (utf8Token.Length > policy.MaximumTokenSizeInBytes)
            {
                error = TokenValidationError.MalformedToken();
                goto TokenAnalyzed;
            }

            Span<TokenSegment> segments = stackalloc TokenSegment[Constants.JweSegmentCount];
            ref TokenSegment segmentsRef = ref MemoryMarshal.GetReference(segments);
            int segmentCount = Tokenizer.Tokenize(utf8Token, ref segmentsRef);
            if (segmentCount < Constants.JwsSegmentCount)
            {
                error = TokenValidationError.MalformedToken();
                goto TokenAnalyzed;
            }

            var headerSegment = segmentsRef;
            if (headerSegment.IsEmpty)
            {
                error = TokenValidationError.MalformedToken();
                goto TokenAnalyzed;
            }

            JwtHeader? header;
            var rawHeader = utf8Token.Slice(0, headerSegment.Length);
            int headerJsonDecodedLength = Base64Url.GetArraySizeRequiredToDecode(rawHeader.Length);
            int payloadjsonDecodedLength;
            int jsonBufferLength;
            if (segmentCount == Constants.JwsSegmentCount)
            {
                payloadjsonDecodedLength = Base64Url.GetArraySizeRequiredToDecode(Unsafe.Add(ref segmentsRef, 1).Length);
                jsonBufferLength = Math.Max(headerJsonDecodedLength, payloadjsonDecodedLength);
            }
            else
            {
                payloadjsonDecodedLength = Base64Url.GetArraySizeRequiredToDecode(Unsafe.Add(ref segmentsRef, 3).Length);
                jsonBufferLength = Math.Max(headerJsonDecodedLength, payloadjsonDecodedLength);
            }

            byte[]? jsonBufferToReturnToPool = null;
            var jsonBuffer = /*jsonBufferLength <= Constants.MaxStackallocBytes
              ? stackalloc byte[jsonBufferLength]
              : */(jsonBufferToReturnToPool = ArrayPool<byte>.Shared.Rent(jsonBufferLength)).AsMemory();
            try
            {
                bool validHeader;
                if (policy.HeaderCache.Enabled)
                {
                    if (!policy.HeaderCache.TryGetHeader(rawHeader, out header))
                    {
                        var buffer = jsonBuffer.Slice(0, headerJsonDecodedLength);
                        Base64Url.Decode(rawHeader, buffer.Span);
                        validHeader = TryReadHeader(buffer.Span, policy, segmentCount, out header, out error);
                        policy.HeaderCache.AddHeader(rawHeader, header);
                    }
                    else
                    {
                        validHeader = policy.TryValidateHeader(header, out error);
                    }
                }
                else
                {
                    var buffer = jsonBuffer.Slice(0, headerJsonDecodedLength);
                    Base64Url.Decode(rawHeader, buffer.Span);
                    validHeader = TryReadHeader(buffer.Span, policy, segmentCount, out header, out error);
                }

                if (validHeader)
                {
                    return segmentCount switch
                    {
                        Constants.JwsSegmentCount => TryReadJws(utf8Token, jsonBuffer.Slice(0, payloadjsonDecodedLength), policy, ref segmentsRef, header, out document),
                        Constants.JweSegmentCount => TryReadJwe(utf8Token, jsonBuffer.Slice(0, payloadjsonDecodedLength).Span, policy, rawHeader, ref segmentsRef, header, out document),
                        _ => InvalidDocument(TokenValidationError.MalformedToken(), out document),
                    };
                }
            }
            catch (FormatException formatException)
            {
                error = TokenValidationError.MalformedToken(formatException);
                goto TokenAnalyzed;
            }
            catch (JsonException readerException)
            {
                error = TokenValidationError.MalformedToken(readerException);
                goto TokenAnalyzed;
            }
            catch (InvalidOperationException invalidOperationException) when (invalidOperationException.InnerException is DecoderFallbackException)
            {
                error = TokenValidationError.MalformedToken(invalidOperationException);
                goto TokenAnalyzed;
            }
            finally
            {
                if (jsonBufferToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(jsonBufferToReturnToPool);
                }
            }

        TokenAnalyzed:
            return InvalidDocument(error, out document);

            static bool InvalidDocument(TokenValidationError error, out JwtDocument2 document)
            {
                document = new JwtDocument2(error);
                return false;
            }
        }

        internal static bool TryReadBase64Header(ReadOnlySpan<byte> utf8Header, TokenValidationPolicy policy, int segmentCount, out JwtHeader header, out TokenValidationError error)
        {
            int headerJsonDecodedLength = Base64Url.GetArraySizeRequiredToDecode((int)utf8Header.Length);
            var headerBufferToReturnToPool = ArrayPool<byte>.Shared.Rent(headerJsonDecodedLength);
            try
            {
                Base64Url.Decode(utf8Header, headerBufferToReturnToPool);
                return TryReadHeader(new ReadOnlySpan<byte>(headerBufferToReturnToPool, 0, headerJsonDecodedLength), policy, segmentCount, out header, out error);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBufferToReturnToPool);
            }
        }

        internal static bool TryReadHeader(ReadOnlySpan<byte> utf8Header, TokenValidationPolicy policy, int segmentCount, out JwtHeader header, [NotNullWhen(false)] out TokenValidationError? error)
        {
            header = new JwtHeader();
            bool result;
            var reader = new JwtHeaderReader(utf8Header, policy);
            if (reader.ReadFirstBytes())
            {
                while (reader.Read())
                {
                    var name = reader.TokenName;
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            header.Inner.Add(name, reader.GetJwtObject());
                            break;
                        case JsonTokenType.StartArray:
                            if (reader.TokenName.Length == 4 && IntegerMarshal.ReadUInt32(reader.TokenName) == (uint)JwtHeaderParameters.Crit)
                            {
                                var crit = reader.GetCriticalHeaders();
                                header.Inner.Add(name, new JwtArray(crit.Item1));
                                header.CriticalHeaderHandlers = crit.Item2;
                                continue;
                            }

                            header.Inner.Add(name, reader.GetJwtArray());
                            break;
                        case JsonTokenType.String:
                            if (reader.TokenName.Length == 3)
                            {
                                switch ((JwtHeaderParameters)IntegerMarshal.ReadUInt24(reader.TokenName))
                                {
                                    case JwtHeaderParameters.Alg:
                                        if (segmentCount == Constants.JwsSegmentCount)
                                        {
                                            header.SignatureAlgorithm = reader.GetSignatureAlgorithm();
                                        }
                                        else if (segmentCount == Constants.JweSegmentCount)
                                        {
                                            header.KeyManagementAlgorithm = reader.GetKeyManagementAlgorithm();
                                        }
                                        continue;
                                    case JwtHeaderParameters.Enc:
                                        header.EncryptionAlgorithm = reader.GetEncryptionAlgorithm();
                                        continue;
                                    case JwtHeaderParameters.Zip:
                                        header.CompressionAlgorithm = reader.GetCompressionAlgorithm();
                                        continue;
                                    case JwtHeaderParameters.Cty:
                                        header.Cty = reader.GetString();
                                        continue;
                                    case JwtHeaderParameters.Typ:
                                        header.Typ = reader.GetString();
                                        continue;
                                    case JwtHeaderParameters.Kid:
                                        header.Kid = reader.GetString();
                                        continue;
                                }
                            }

                            header.Inner.Add(name, reader.GetString()!);
                            break;
                        case JsonTokenType.True:
                            header.Inner.Add(name, true);
                            break;
                        case JsonTokenType.False:
                            header.Inner.Add(name, false);
                            break;
                        case JsonTokenType.Null:
                            header.Inner.Add(name);
                            break;
                        case JsonTokenType.Number:
                            if (reader.TryGetInt64(out long longValue))
                            {
                                header.Inner.Add(name, longValue);
                            }
                            else
                            {
                                header.Inner.Add(name, reader.GetDouble());
                            }
                            break;
                    }
                }

                if (reader.TokenType is JsonTokenType.EndObject)
                {
                    result = reader.TryValidateHeader(header, out error);
                }
                else
                {
                    result = false;
                    error = TokenValidationError.MalformedToken();
                }
            }
            else
            {
                result = false;
                error = TokenValidationError.MalformedToken();
            }

            return result;
        }

        private static bool TryReadJws(
            ReadOnlySpan<byte> utf8Buffer,
            Memory<byte> jsonBuffer,
            TokenValidationPolicy policy,
            ref TokenSegment segments,
            JwtHeader header,
            out JwtDocument2? jwt)
        {
            TokenSegment headerSegment = segments;
            TokenSegment payloadSegment = Unsafe.Add(ref segments, 1);
            TokenSegment signatureSegment = Unsafe.Add(ref segments, 2);
            var rawPayload = utf8Buffer.Slice(payloadSegment.Start, payloadSegment.Length);
            var result = policy.TryValidateSignature(header, utf8Buffer.Slice(headerSegment.Start, headerSegment.Length + payloadSegment.Length + 1), utf8Buffer.Slice(signatureSegment.Start, signatureSegment.Length));
            if (!result.Succedeed)
            {
                jwt = new JwtDocument2(TokenValidationError.SignatureValidationFailed(result));
                return false;
            }

            Exception malformedException;
            try
            {
                Base64Url.Decode(rawPayload, jsonBuffer.Span);
                if (TryReadPayload(jsonBuffer, policy, out JwtPayloadDocument? payload, out TokenValidationError? error))
                {
                    if (policy.TryValidateJwt(header, payload, out error))
                    {
                        jwt = new JwtDocument2(header, payload);
                        return true;
                    }
                    else
                    {
                        jwt = new JwtDocument2(header, payload, error);
                        return false;
                    }
                }
                else
                {
                    jwt = new JwtDocument2(error);
                    return false;
                }
            }
            catch (FormatException formatException)
            {
                malformedException = formatException;
                goto Malformed;
            }
            catch (JsonException readerException)
            {
                malformedException = readerException;
                goto Malformed;
            }
            catch (InvalidOperationException invalidOperationException) when (invalidOperationException.InnerException is DecoderFallbackException)
            {
                malformedException = invalidOperationException;
                goto Malformed;
            }


        Malformed:
            jwt = new JwtDocument2(TokenValidationError.MalformedToken(exception: malformedException));
            return false;
        }

        private static bool TryReadJwe(
            ReadOnlySpan<byte> utf8Buffer,
            Span<byte> jsonBuffer,
            TokenValidationPolicy policy,
            ReadOnlySpan<byte> rawHeader,
            ref TokenSegment segments,
            JwtHeader header,
            out JwtDocument2 document)
        {
            TokenValidationError error;
            TokenSegment encryptionKeySegment = Unsafe.Add(ref segments, 1);
            TokenSegment ivSegment = Unsafe.Add(ref segments, 2);
            TokenSegment ciphertextSegment = Unsafe.Add(ref segments, 3);
            TokenSegment authenticationTagSegment = Unsafe.Add(ref segments, 4);
            var enc = header.EncryptionAlgorithm;
            if (enc is null)
            {
                error = TokenValidationError.MissingEncryptionAlgorithm();
                goto Error;
            }

            if (!JwtReaderHelper.TryGetContentEncryptionKeys(header, utf8Buffer.Slice(encryptionKeySegment.Start, encryptionKeySegment.Length), enc, policy.DecryptionKeyProviders, out var keys))
            {
                error = TokenValidationError.EncryptionKeyNotFound();
                goto Error;
            }

            var rawInitializationVector = utf8Buffer.Slice(ivSegment.Start, ivSegment.Length);
            var rawCiphertext = utf8Buffer.Slice(ciphertextSegment.Start, ciphertextSegment.Length);
            var rawAuthenticationTag = utf8Buffer.Slice(authenticationTagSegment.Start, authenticationTagSegment.Length);

            int decryptedLength = Base64Url.GetArraySizeRequiredToDecode(rawCiphertext.Length);
            byte[]? decryptedArrayToReturnToPool = null;
            Span<byte> decryptedBytes = /*decryptedLength <= Constants.MaxStackallocBytes
                  ? stackalloc byte[decryptedLength]
                  : */(decryptedArrayToReturnToPool = ArrayPool<byte>.Shared.Rent(decryptedLength));

            try
            {
                if (JwtReaderHelper.TryDecryptToken(keys, rawHeader, rawCiphertext, rawInitializationVector, rawAuthenticationTag, enc, decryptedBytes, out SymmetricJwk? decryptionKey, out int bytesWritten))
                {
                    decryptedBytes = decryptedBytes.Slice(0, bytesWritten);
                }
                else
                {
                    error = TokenValidationError.DecryptionFailed();
                    goto Error;
                }

                bool compressed;
                ReadOnlySequence<byte> decompressedBytes = default;
                var zip = header.CompressionAlgorithm;
                if (zip is null)
                {
                    compressed = false;
                }
                else
                {
                    Compressor compressor = zip.Compressor;
                    if (compressor is null)
                    {
                        error = TokenValidationError.InvalidHeader(HeaderParameters.ZipUtf8);
                        goto Error;
                    }

                    try
                    {
                        compressed = true;
                        decompressedBytes = compressor.Decompress(decryptedBytes);
                    }
                    catch (Exception e)
                    {
                        error = TokenValidationError.DecompressionFailed(e);
                        goto Error;
                    }
                }

                JwtDocument2 jwe;
                if (policy.IgnoreNestedToken)
                {
                    var rawValue = compressed
                        ? decompressedBytes.IsSingleSegment
                            ? decompressedBytes.First
                            : decompressedBytes.ToArray()
                        : decryptedArrayToReturnToPool.AsMemory(0, decryptedBytes.Length);
                    jwe = new JwtDocument2(header, rawValue, decryptedArrayToReturnToPool);
                    decryptedArrayToReturnToPool = null; // do not return to the pool
                }
                else
                {
                    bool decrypted = compressed
                        ? TryParse(decompressedBytes, policy, out var nestedDocument)
                        : TryParse(decryptedBytes, policy, out nestedDocument);
                    if (decrypted)
                    {
                        jwe = new JwtDocument2(header, nestedDocument.Payload);
                    }
                    else
                    {
                        if (nestedDocument.Error.Status == TokenValidationStatus.MalformedToken)
                        {
                            // The decrypted payload is not a nested JWT
                            var rawValue = compressed
                               ? decompressedBytes.IsSingleSegment
                                   ? decompressedBytes.First
                                   : decompressedBytes.ToArray()
                               : decryptedArrayToReturnToPool.AsMemory(0, decryptedBytes.Length);
                            jwe = new JwtDocument2(header, rawValue, decryptedArrayToReturnToPool);
                            decryptedArrayToReturnToPool = null; // do not return to the pool
                        }
                        else
                        {
                            jwe = new JwtDocument2(header, nestedDocument.Payload, nestedDocument.Error);
                        }
                    }
                }

                document = jwe;
                return true;
            }
            finally
            {
                if (decryptedArrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(decryptedArrayToReturnToPool);
                }
            }

        Error:
            document = new JwtDocument2(error);
            return false;
        }

        public static bool TryReadPayload(ReadOnlyMemory<byte> utf8Payload, TokenValidationPolicy policy, [NotNullWhen(true)] out JwtPayloadDocument? payload, [NotNullWhen(false)] out TokenValidationError? error)
        {
            try
            {
                payload = new JwtPayloadDocument(JsonDocument.Parse(utf8Payload));
                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = TokenValidationError.MalformedToken(e);
                payload = null;
                return false;
            }
        }

        public static bool TryParse(ReadOnlySequence<byte> utf8Token, TokenValidationPolicy policy, out JwtDocument2 document)
        {
            if (utf8Token.IsSingleSegment)
            {
                return TryParse(utf8Token.First.Span, policy, out document);
            }

            return TryParse(utf8Token.ToArray(), policy, out document);
        }

        /// <summary>
        /// Reads and validates a JWT encoded as a JWS or JWE in compact serialized format.
        /// </summary>
        /// <param name="token">The JWT encoded as JWE or JWS</param>
        /// <param name="policy">The validation policy.</param>
        public static bool TryParse(string token, TokenValidationPolicy policy, out JwtDocument2 document)
        {
            if (token is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.token);
            }

            if (policy is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.policy);
            }

            if (token.Length == 0)
            {
                document = new JwtDocument2(TokenValidationError.MalformedToken());
                return false;
            }

            int length = Utf8.GetMaxByteCount(token.Length);
            if (length > policy.MaximumTokenSizeInBytes)
            {
                document = new JwtDocument2(TokenValidationError.MalformedToken());
                return false;
            }

            byte[]? utf8ArrayToReturnToPool = null;
            var utf8Token = length <= Constants.MaxStackallocBytes
                  ? stackalloc byte[length]
                  : (utf8ArrayToReturnToPool = ArrayPool<byte>.Shared.Rent(length));
            try
            {
                int bytesWritten = Utf8.GetBytes(token, utf8Token);
                return TryParse(utf8Token.Slice(0, bytesWritten), policy, out document);
            }
            finally
            {
                if (utf8ArrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(utf8ArrayToReturnToPool);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _rawValue = ReadOnlyMemory<byte>.Empty;
            byte[]? rented = Interlocked.Exchange(ref _rented, null);
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}