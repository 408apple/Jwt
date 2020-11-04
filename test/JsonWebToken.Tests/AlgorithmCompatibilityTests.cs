﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace JsonWebToken.Tests
{
    public class JwtXTests
    {
        [Fact]
        public void Create()
        {
            var jwt = new JwtObjectX
            {
                { "prop1", "value1" },
                { "prop2",  new Dictionary<string, object> { { "prop1", "value2" } } },
                { "prop3", 123L },
                { "prop4", new Fake { Inner = new Fake { Value = "Inner1", Inner = new Fake { Value = "Inner2" } }, Value = "Inner0" } },
                { "prop5", new [] { "a", "b", "c"} },
                { "prop6", new [] { new object(), new object(), "abc", 123 } }
            };

            jwt.Flush();
        }

        [Fact]
        public void Descriptor()
        {
            var descriptor = new JwsDescriptorX
            {
                Alg = SignatureAlgorithm.HmacSha256,
                SigningKey = SymmetricJwk.GenerateKey(256), 
                Header = new JwtHeaderX
                {
                    { "prop1", "value1" },
                    { "prop2",  new Dictionary<string, object> { { "prop1", "value2" } } },
                    { "prop3", 123L },
                    { "prop4", new Fake { Inner = new Fake { Value = "Inner1", Inner = new Fake { Value = "Inner2" } }, Value = "Inner0" } },
                    { "prop5", new [] { "a", "b", "c"} },
                    { "prop6", new [] { new object(), new object(), "abc", 123 } }
                },
                Payload = new JwtPayloadX
                {
                    { "prop1", "value1" },
                    { "prop2",  new Dictionary<string, object> { { "prop1", "value2" } } },
                    { "prop3", 123L },
                    { "prop4", new Fake { Inner = new Fake { Value = "Inner1", Inner = new Fake { Value = "Inner2" } }, Value = "Inner0" } },
                    { "prop5", new [] { "a", "b", "c"} },
                    { "prop6", new [] { new object(), new object(), "abc", 123 } }
                }
            };

            descriptor.Header.TryGetValue("prop1", out var tokenType);
            Assert.Equal(JsonValueKind.String, tokenType.Type);
            descriptor.Header.TryGetValue("prop2", out tokenType);
            Assert.Equal(JsonValueKind.Object, tokenType.Type);
            descriptor.Header.TryGetValue("prop3", out tokenType);
            Assert.Equal(JsonValueKind.Number, tokenType.Type);
            descriptor.Header.TryGetValue("prop4", out tokenType);
            Assert.Equal(JsonValueKind.Object, tokenType.Type);
            descriptor.Header.TryGetValue("prop5", out tokenType);
            Assert.Equal(JsonValueKind.Array, tokenType.Type);
            descriptor.Header.TryGetValue("prop6", out tokenType);
            Assert.Equal(JsonValueKind.Array, tokenType.Type);

            descriptor.Header.TryGetValue("prop1", out var value1);
            Assert.Equal("value1", (string)value1.Value);

            PooledByteBufferWriter writer = new PooledByteBufferWriter();
            var context = new EncodingContext(writer, null, 0, false);
            descriptor.Encode(context);
        }


        public class Fake
        {
            public Fake Inner { get; set; }

            public string Value { get; set; }
        }
    }

    public class AlgorithmCompatibilityTests : IClassFixture<KeyFixture>
    {
        public AlgorithmCompatibilityTests(KeyFixture keys)
        {
            _keys = keys;
        }

        private static readonly SymmetricJwk _signingKey = SymmetricJwk.GenerateKey(256, SignatureAlgorithm.HmacSha256);
        private readonly KeyFixture _keys;

        [Theory]
        [MemberData(nameof(GetCompatibleAlgorithms))]
        public void Compatible(EncryptionAlgorithm enc, KeyManagementAlgorithm alg)
        {
            var writer = new JwtWriter();
            foreach (var encryptionKey in SelectEncryptionKey(enc.Name, alg.Name))
            {
                var descriptor = new JweDescriptor
                {
                    EncryptionKey = encryptionKey,
                    EncryptionAlgorithm = enc,
                    Algorithm = alg,
                    Payload = new JwsDescriptor
                    {
                        SigningKey = _signingKey,
                        Algorithm = SignatureAlgorithm.HmacSha256,
                        Subject = "Alice"
                    }
                };

                var token = writer.WriteToken(descriptor);

                var policy = new TokenValidationPolicyBuilder()
                    .RequireSignature(_signingKey)
                    .WithDecryptionKeys(_keys.Jwks)
                    .Build();

                var result = Jwt.TryParse(token, policy, out var jwt);
                Assert.True(result);
                Assert.True(jwt.Payload.TryGetClaim("sub", out var sub));
                Assert.Equal("Alice", sub.GetString());
                jwt.Dispose();
            }
        }

        private IEnumerable<Jwk> SelectEncryptionKey(string enc, string alg)
        {
            switch (alg)
            {
                case "A128KW":
                case "A128GCMKW":
                    yield return _keys.Symmetric128Key;
                    break;
                case "A192KW":
                case "A192GCMKW":
                    yield return _keys.Symmetric192Key;
                    break;
                case "A256KW":
                case "A256GCMKW":
                    yield return _keys.Symmetric256Key;
                    break;
                case "dir":
                    switch (enc)
                    {
                        case "A128CBC-HS256":
                            yield return _keys.Symmetric256Key;
                            break;
                        case "A192CBC-HS384":
                            yield return _keys.Symmetric384Key;
                            break;
                        case "A256CBC-HS512":
                            yield return _keys.Symmetric512Key;
                            break;
                        case "A128GCM":
                            yield return _keys.Symmetric128Key;
                            break;
                        case "A192GCM":
                            yield return _keys.Symmetric192Key;
                            break;
                        case "A256GCM":
                            yield return _keys.Symmetric256Key;
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                    break;
                case "RSA-OAEP":
                case "RSA-OAEP-256":
                case "RSA-OAEP-384":
                case "RSA-OAEP-512":
                case "RSA1_5":
                    yield return _keys.PrivateRsa2048Key;
                    break;
#if !NET461 && !NET47
                case "ECDH-ES+A128KW":
                case "ECDH-ES+A192KW":
                case "ECDH-ES+A256KW":
                    yield return _keys.PrivateEcc256Key;
                    yield return _keys.PrivateEcc384Key;
                    yield return _keys.PrivateEcc512Key;
                    break;
                case "ECDH-ES":
                    yield return _keys.PrivateEcc256Key;
                    yield return _keys.PrivateEcc384Key;
                    yield return _keys.PrivateEcc512Key;
                    break;
#endif
                default:
                    throw new NotSupportedException();
            }

            yield break;
        }

        public static IEnumerable<object[]> GetCompatibleAlgorithms()
        {
            foreach (var enc in GetEncryptionAlgorithms())
            {
                foreach (var alg in GetKeyManagementAlgorithms())
                {
                    yield return new object[] { enc, alg };
                }
            }
        }

        public static IEnumerable<KeyManagementAlgorithm> GetKeyManagementAlgorithms()
        {
            //            yield return KeyManagementAlgorithm.Aes128KW;
            //            yield return KeyManagementAlgorithm.Aes192KW;
            //            yield return KeyManagementAlgorithm.Aes256KW;
            yield return KeyManagementAlgorithm.Direct;

            //#if NETCOREAPP3_0
            //            yield return KeyManagementAlgorithm.Aes128GcmKW;
            //            yield return KeyManagementAlgorithm.Aes192GcmKW;
            //            yield return KeyManagementAlgorithm.Aes256GcmKW;
            //#endif
            //            yield return KeyManagementAlgorithm.RsaOaep;
            //            yield return KeyManagementAlgorithm.RsaPkcs1;
            //            yield return KeyManagementAlgorithm.RsaOaep256;
            //            yield return KeyManagementAlgorithm.RsaOaep384;
            //            yield return KeyManagementAlgorithm.RsaOaep512;
            //#if NETCOREAPP
            //            yield return KeyManagementAlgorithm.EcdhEs;
            //            yield return KeyManagementAlgorithm.EcdhEsAes128KW;
            //            yield return KeyManagementAlgorithm.EcdhEsAes192KW;
            //            yield return KeyManagementAlgorithm.EcdhEsAes256KW;
            //#endif
        }

        private static IEnumerable<EncryptionAlgorithm> GetEncryptionAlgorithms()
        {
            yield return EncryptionAlgorithm.Aes128CbcHmacSha256;
            //            yield return EncryptionAlgorithm.Aes192CbcHmacSha384;
            //            yield return EncryptionAlgorithm.Aes256CbcHmacSha512;
            //#if NETCOREAPP3_0
            //            yield return EncryptionAlgorithm.Aes128Gcm;
            //            yield return EncryptionAlgorithm.Aes192Gcm;
            //            yield return EncryptionAlgorithm.Aes256Gcm;
            //#endif
        }
    }
}