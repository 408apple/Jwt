﻿using System.Threading;

namespace JsonWebTokens
{
    public interface IConfigurationRetriever<T>
    {
        /// <summary>
        /// Retrieves a populated configuration given an address and an <see cref="DocumentRetriever"/>.
        /// </summary>
        /// <param name="address">Address of the discovery document.</param>
        /// <param name="retriever">The <see cref="IDocumentRetriever"/> to use to read the discovery document.</param>
        /// <param name="cancel">A cancellation token that can be used by other objects or threads to receive notice of cancellation. <see cref="CancellationToken"/>.</param>
        T GetConfiguration(string address, HttpDocumentRetriever retriever, CancellationToken cancellationToken);
    }
}
