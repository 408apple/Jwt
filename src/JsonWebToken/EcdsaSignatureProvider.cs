﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace JsonWebToken
{
    public class EcdsaSignatureProvider : AsymmetricSignatureProvider<EcdsaJwk>
    {
        private ECDsa _ecdsa;
        private HashAlgorithmName _hashAlgorithm;
        private bool _disposed;

        private static readonly IReadOnlyDictionary<string, int> DefaultMinimumKeySizeInBitsForSigning = new Dictionary<string, int>()
        {
            { SecurityAlgorithms.EcdsaSha256, 256 },
            { SecurityAlgorithms.EcdsaSha384, 256 },
            { SecurityAlgorithms.EcdsaSha512, 256 },
        };

        private static readonly IReadOnlyDictionary<string, int> DefaultMinimumKeySizeInBitsForVerifying = new Dictionary<string, int>()
        {
            { SecurityAlgorithms.EcdsaSha256, 256 },
            { SecurityAlgorithms.EcdsaSha384, 256 },
            { SecurityAlgorithms.EcdsaSha512, 256 },
        };

        public override IReadOnlyDictionary<string, int> MinimumKeySizeInBitsForSigning => DefaultMinimumKeySizeInBitsForSigning;

        public override IReadOnlyDictionary<string, int> MinimumKeySizeInBitsForVerifying => DefaultMinimumKeySizeInBitsForVerifying;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsymmetricSignatureProvider"/> class used to create and verify signatures.
        /// </summary>
        /// <param name="key">The <see cref="JsonWebKey"/> that will be used for signature operations.</param>
        /// <param name="algorithm">The signature algorithm to apply.</param>
        /// <param name="willCreateSignatures">Whether this <see cref="AsymmetricSignatureProvider"/> is required to create signatures then set this to true.</param>
        public EcdsaSignatureProvider(EcdsaJwk key, string algorithm, bool willCreateSignatures)
            : base(key, algorithm, willCreateSignatures)
        {
        }

        private HashAlgorithmName GetHashAlgorithmName(string algorithm)
        {
            if (string.IsNullOrWhiteSpace(algorithm))
            {
                throw new ArgumentNullException(nameof(algorithm));
            }

            switch (algorithm)
            {
                case SecurityAlgorithms.EcdsaSha256:
                    return HashAlgorithmName.SHA256;

                case SecurityAlgorithms.EcdsaSha384:
                    return HashAlgorithmName.SHA384;

                case SecurityAlgorithms.EcdsaSha512:
                    return HashAlgorithmName.SHA512;
            }

            throw new ArgumentOutOfRangeException(nameof(algorithm), ErrorMessages.FormatInvariant(ErrorMessages.NotSupportedAlgorithm, algorithm));
        }

        protected override void ResolveAsymmetricAlgorithm(EcdsaJwk key, string algorithm, bool willCreateSignatures)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(algorithm))
            {
                throw new ArgumentNullException(nameof(algorithm));
            }

            _hashAlgorithm = GetHashAlgorithmName(algorithm);
            _ecdsa = ResolveAlgorithm(key, algorithm, willCreateSignatures);
        }

        /// <summary>
        /// Produces a signature over the 'input' using the <see cref="ASymmetricJwk"/> and algorithm passed to <see cref="AsymmetricSignatureProvider( JsonWebKey, string, bool )"/>.
        /// </summary>
        /// <param name="input">The bytes to be signed.</param>
        /// <returns>A signature over the input.</returns>
        public override byte[] Sign(byte[] input)
        {
            if (input == null || input.Length == 0)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().ToString());
            }

            if (_ecdsa == null)
            {
                throw new InvalidOperationException(ErrorMessages.FormatInvariant(ErrorMessages.NotSupportedUnwrap, _hashAlgorithm));
            }

            return _ecdsa.SignData(input, _hashAlgorithm);
        }

        /// <summary>
        /// Verifies that a signature over the' input' matches the signature.
        /// </summary>
        /// <param name="input">The bytes to generate the signature over.</param>
        /// <param name="signature">The value to verify against.</param>
        /// <returns>true if signature matches, false otherwise.</returns>
        public override bool Verify(byte[] input, byte[] signature)
        {
            if (input == null || input.Length == 0)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (signature == null || signature.Length == 0)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().ToString());
            }

            if (_ecdsa == null)
            {
                throw new InvalidOperationException(ErrorMessages.NotSupportedUnwrap);
            }

            return _ecdsa.VerifyData(input, signature, _hashAlgorithm);
        }

        /// <summary>
        /// Calls <see cref="HashAlgorithm.Dispose()"/> to release this managed resources.
        /// </summary>
        /// <param name="disposing">true, if called from Dispose(), false, if invoked inside a finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    if (_ecdsa != null)
                    {
                        _ecdsa.Dispose();
                    }
                }
            }
        }

        private static ECDsaCng ResolveAlgorithm(EcdsaJwk key, string algorithm, bool usePrivateKey)
        { 
            return key.CreateECDsa(algorithm, usePrivateKey);
        }
    }
}


