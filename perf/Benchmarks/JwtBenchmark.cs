﻿using BenchmarkDotNet.Attributes;
using JsonWebToken.Internal;
using System;
using System.Collections.Generic;

namespace JsonWebToken.Performance
{
    public class JwsBenchmark : JwtBenchmarkBase
    {
        public IEnumerable<string> GetTokens()
        {
            yield return "JWS-empty";
            yield return "JWS-small";
            yield return "JWS-medium";
            yield return "JWS-big";
        }

        [Benchmark(OperationsPerInvoke = OperationPerInvoke)]
        [ArgumentsSource(nameof(GetTokens))]
        public override void ValidateJwt(string token)
        {
            for (int i = 0; i < OperationPerInvoke; i++)
            {
                ValidateJwtCore(token, policyWithSignatureValidation);
            }
        }

        [Benchmark(OperationsPerInvoke = OperationPerInvoke)]
        [ArgumentsSource(nameof(GetTokens))]
        public override void WriteJwt(string token)
        {
            for (int i = 0; i < OperationPerInvoke; i++)
            {
                WriteJwtCore(token);
            }
        }
    }

    public class JwtBenchmark : JwtBenchmarkBase
    {
        public IEnumerable<string> GetTokens()
        {
            yield return "JWT-empty";
            yield return "JWT-small";
            yield return "JWT-medium";
            yield return "JWT-big";
        }

        [Benchmark(OperationsPerInvoke = OperationPerInvoke)]
        [ArgumentsSource(nameof(GetTokens))]
        public override void ValidateJwt(string token)
        {
            for (int i = 0; i < OperationPerInvoke; i++)
            {
                ValidateJwtCore(token, TokenValidationPolicy.NoValidation);
            }
        }

        [Benchmark(OperationsPerInvoke = OperationPerInvoke)]
        [ArgumentsSource(nameof(GetTokens))]
        public override void WriteJwt(string token)
        {
            for (int i = 0; i < OperationPerInvoke; i++)
            {
                WriteJwtCore(token);
            }
        }
    }

    public class JweBenchmark : JwtBenchmarkBase
    {
        public IEnumerable<string> GetTokens()
        {
            yield return "JWE-empty";
            yield return "JWE-small";
            yield return "JWE-medium";
            yield return "JWE-big";
        }

        [Benchmark(OperationsPerInvoke = OperationPerInvoke)]
        [ArgumentsSource(nameof(GetTokens))]
        public override void ValidateJwt(string token)
        {
            for (int i = 0; i < OperationPerInvoke; i++)
            {
                ValidateJwtCore(token, policyWithSignatureValidation);
            }
        }

        [Benchmark(OperationsPerInvoke = OperationPerInvoke)]
        [ArgumentsSource(nameof(GetTokens))]
        public override void WriteJwt(string token)
        {
            for (int i = 0; i < OperationPerInvoke; i++)
            {
                WriteJwtCore(token);
            }
        }
    }

    public class JweCompressedBenchmark : JwtBenchmarkBase
    {
        public IEnumerable<string> GetTokens()
        {
            yield return "JWE-DEF-empty";
            yield return "JWE-DEF-small";
            yield return "JWE-DEF-medium";
            yield return "JWE-DEF-big";
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetTokens))]
        public override void ValidateJwt(string token)
        {
            ValidateJwtCore(token, policyWithSignatureValidation);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetTokens))]
        public override void WriteJwt(string token)
        {
            WriteJwtCore(token);
        }
    }

    [Config(typeof(AllTfmCoreConfig))]
    public abstract class JwtBenchmarkBase
    {
        protected const int OperationPerInvoke = 500;

        private static readonly SymmetricJwk SigningKey = Tokens.SigningKey;

        private static readonly SymmetricJwk EncryptionKey = Tokens.EncryptionKey;

        private static readonly Microsoft.IdentityModel.Tokens.JsonWebKey WilsonSharedKey = Microsoft.IdentityModel.Tokens.JsonWebKey.Create(SigningKey.ToString());

        public static readonly JwtWriter Writer = new JwtWriter();
        public static readonly JwtReader Reader = new JwtReader(Tokens.EncryptionKey);
        protected static readonly TokenValidationPolicy policyWithSignatureValidation = new TokenValidationPolicyBuilder().RequireSignature(Tokens.SigningKey).Build();

        private static readonly Dictionary<string, JwtDescriptor> JwtPayloads = CreateJwtDescriptors();

        public abstract void WriteJwt(string token);

        public abstract void ValidateJwt(string token);

        //[Benchmark]
        //[ArgumentsSource(nameof(GetTokens))]
        protected void WriteJwtCore(string token)
        {
            var value = Writer.WriteToken(JwtPayloads[token]);
        }

        //[Benchmark]
        //[ArgumentsSource(nameof(GetTokens))]
        protected void ValidateJwtCore(string token, TokenValidationPolicy policy)
        {
            var result = Reader.TryReadToken(Tokens.ValidBinaryTokens[token], policy);
            if (!result.Succedeed)
            {
                throw new Exception(result.Status.ToString());
            }
        }

        //[Benchmark(Baseline = true)]
        //[ArgumentsSource(nameof(GetTokens))]
        //public void ValidateJwt_Cache(string token)
        //{
        //    Reader.EnableHeaderCaching = true;
        //    var result = Reader.TryReadToken(Tokens.ValidTokens[token].AsSpan(), policy);
        //    if (!result.Succedeed)
        //    {
        //        throw new Exception(result.Status.ToString());
        //    }
        //}

        //public IEnumerable<object[]> GetTokens()
        //{
        //    yield return new[] { "JWS-empty" };
        //    //yield return new[] { "JWS-small" };
        //    //yield return new[] { "JWS-medium" };
        //    //yield return new[] { "JWS-big" };
        //    //yield return new[] { "JWE-empty" };
        //    //yield return new[] { "JWE-small" };
        //    //yield return new[] { "JWE-medium" };
        //    //yield return new[] { "JWE-big" };
        //    //yield return new[] { "JWE-DEF-empty" };
        //    //yield return new[] { "JWE-DEF-small" };
        //    //yield return new[] { "JWE-DEF-medium" };
        //    //yield return new[] { "JWE-DEF-big" };
        //}

        private static Dictionary<string, JwtDescriptor> CreateJwtDescriptors()
        {
            var descriptors = new Dictionary<string, JwtDescriptor>();
            foreach (var payload in Tokens.Payloads)
            {
                var descriptor = new JwsDescriptor()
                {
                   Algorithm = SignatureAlgorithm.None
                };

                foreach (var property in payload.Value.Properties())
                {
                    switch (property.Name)
                    {
                        case "iat":
                        case "nbf":
                        case "exp":
                            descriptor.AddClaim(property.Name, EpochTime.ToDateTime((long)property.Value));
                            break;
                        default:
                            descriptor.AddClaim(property.Name, (string)property.Value);
                            break;
                    }
                }

                descriptors.Add("JWT-" + payload.Key, descriptor);
            }

            foreach (var payload in Tokens.Payloads)
            {
                var descriptor = new JwsDescriptor()
                {
                    Key = SigningKey
                };

                foreach (var property in payload.Value.Properties())
                {
                    switch (property.Name)
                    {
                        case "iat":
                        case "nbf":
                        case "exp":
                            descriptor.AddClaim(property.Name, EpochTime.ToDateTime((long)property.Value));
                            break;
                        default:
                            descriptor.AddClaim(property.Name, (string)property.Value);
                            break;
                    }
                }

                descriptors.Add("JWS-" + payload.Key, descriptor);
            }

            foreach (var payload in Tokens.Payloads)
            {
                foreach (var compression in new[] { null, CompressionAlgorithm.Deflate })
                {
                    var descriptor = new JwsDescriptor()
                    {
                        Key = SigningKey
                    };

                    foreach (var property in payload.Value.Properties())
                    {
                        switch (property.Name)
                        {
                            case "iat":
                            case "nbf":
                            case "exp":
                                descriptor.AddClaim(property.Name, EpochTime.ToDateTime((long)property.Value));
                                break;
                            default:
                                descriptor.AddClaim(property.Name, (string)property.Value);
                                break;
                        }
                    }

                    var jwe = new JweDescriptor
                    {
                        Payload = descriptor,
                        Key = EncryptionKey,
                        EncryptionAlgorithm = EncryptionAlgorithm.Aes128CbcHmacSha256,
                        CompressionAlgorithm = compression
                    };

                    var descriptorName = "JWE-";
                    if (compression != null)
                    {
                        descriptorName += compression + "-";
                    }

                    descriptorName += payload.Key;
                    descriptors.Add(descriptorName, jwe);
                }
            }

            return descriptors;
        }
    }
}