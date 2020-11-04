﻿using System;
using JsonWebToken;

namespace ValidatePerf
{
    class Program
    {
        private static readonly Jwk signingKey = SymmetricJwk.GenerateKey(128, SignatureAlgorithm.HmacSha256);
        private static readonly JwtWriter _writer = new JwtWriter();
        private static readonly JwsDescriptor jwsDescriptor = new JwsDescriptor()
        {
            IssuedAt = new DateTime(2017, 7, 14, 4, 40, 0, DateTimeKind.Utc),
            ExpirationTime = new DateTime(2033, 5, 18, 5, 33, 20, DateTimeKind.Utc),
            Issuer = "https://idp.example.com/",
            Audience = "636C69656E745F6964",
            SigningKey = signingKey
        };
        private static readonly JwsDescriptorX jwsDescriptorX = new JwsDescriptorX()
        {
            SigningKey = signingKey,
            Alg = SignatureAlgorithm.HmacSha256,
            Payload = new JwtPayloadX
            {
                { "iat", 1500000000L },
                { "exp", 2000000000L },
                { "iss", "https://idp.example.com/" },
                { "aud", "636C69656E745F6964" }
            }
        };
        private static readonly byte[] jwsToken = _writer.WriteToken(jwsDescriptor);

        private static readonly Jwk encryptionKey = SymmetricJwk.GenerateKey(256, KeyManagementAlgorithm.Aes256KW);
        private static JweDescriptor jweDescriptor = new JweDescriptor
        {
            Payload = new JwsDescriptor
            {
                IssuedAt = new DateTime(2017, 7, 14, 4, 40, 0, DateTimeKind.Utc),
                ExpirationTime = new DateTime(2033, 5, 18, 5, 33, 20, DateTimeKind.Utc),
                Issuer = "https://idp.example.com/",
                Audience = "636C69656E745F6964",
                SigningKey = signingKey
            },
            EncryptionKey = encryptionKey,
            EncryptionAlgorithm = EncryptionAlgorithm.Aes128CbcHmacSha256
        };
        private static readonly byte[] jweToken = _writer.WriteToken(jweDescriptor);

        private static void Main()
        {
            var policy = new TokenValidationPolicyBuilder()
                .RequireSignature(signingKey)
                .RequireAudience("636C69656E745F6964")
                .RequireIssuer("https://idp.example.com/")
                .EnableLifetimeValidation()
                .DisabledHeaderCache()
                .Build();

            Console.WriteLine("Starting...");
            while (true)
            {
                Encode1();

                Encode2();
            }
        }

        private static void Encode1()
        {
            JwsDescriptor jwsDescriptor = new JwsDescriptor()
            {
                IssuedAt = new DateTime(2017, 7, 14, 4, 40, 0, DateTimeKind.Utc),
                ExpirationTime = new DateTime(2033, 5, 18, 5, 33, 20, DateTimeKind.Utc),
                Issuer = "https://idp.example.com/",
                Audience = "636C69656E745F6964",
                SigningKey = signingKey
            };
            var bufferWriter1 = new PooledByteBufferWriter();
            var context1 = new EncodingContext(bufferWriter1, null, 0, false);
            jwsDescriptor.Encode(context1);
            bufferWriter1.Dispose();
        }

        private static void Encode2()
        {
            JwsDescriptorX jwsDescriptorX = new JwsDescriptorX()
            {
                SigningKey = signingKey,
                Alg = SignatureAlgorithm.HmacSha256,
                Payload = new JwtPayloadX
                    {
                        { "iat", 1500000000L },
                        { "exp", 2000000000L },
                        { "iss", "https://idp.example.com/" },
                        { "aud", "636C69656E745F6964" }
                    }
            };

            var bufferWriter2 = new PooledByteBufferWriter();
            var context2 = new EncodingContext(bufferWriter2, null, 0, false);
            jwsDescriptorX.Encode(context2);
            bufferWriter2.Dispose();
        }
    }
}
