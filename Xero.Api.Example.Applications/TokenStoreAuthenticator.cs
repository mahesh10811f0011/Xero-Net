﻿using System;
using System.Security.Cryptography.X509Certificates;
using Xero.Api.Infrastructure.Interfaces;
using Xero.Api.Infrastructure.OAuth;

namespace Xero.Api.Example.Applications
{
    public abstract class TokenStoreAuthenticator : IAuthenticator
    {
        private readonly string _tokenUri;
        protected string CallBackUri { get; set; }
        protected string BaseUri { get; set; }
        protected string VerifierUri { get; set; }
        protected ITokenStore Store { get; set; }

        private OAuthTokens _tokens;

        protected OAuthTokens Tokens 
        {
            get 
            {
                if (_tokens == null)
                {
                    _tokens = new OAuthTokens(_tokenUri, BaseUri, GetClientCertificate());      
                }
                return _tokens;
            } 
        }

        protected TokenStoreAuthenticator(string baseUri, string tokenUri, string callBackUri, ITokenStore store)
        {
            _tokenUri = tokenUri;
            CallBackUri = callBackUri;
            BaseUri = baseUri;
            Store = store;                      
        }

        protected virtual X509Certificate2 GetClientCertificate()
        {
            return null;            
        }

        public string GetSignature(IConsumer consumer, IUser user, Uri uri, string verb, IConsumer consumer1)
        {
            return GetAuthorization(GetToken(consumer, user), verb, uri.AbsolutePath, uri.Query);
        }

        public IToken GetToken(IConsumer consumer, IUser user)
        {
            if (!HasStore)
                return GetToken(consumer);

            var token = Store.Find(user.Name);

            if (token == null)
            {
                token = GetToken(consumer);
                token.UserId = user.Name;

                Store.Add(token);

                return token;
            }

            if (!token.HasExpired)
                return token;
            
            var newToken = RenewToken(token, consumer);
            newToken.UserId = user.Name;

            Store.Delete(token);
            Store.Add(newToken);

            return newToken;
        }

        public bool HasStore
        {
            get { return Store != null; }
        }

        public IUser User { get; set; }

        protected abstract string AuthorizeUser(IToken oauthToken);
        protected abstract string CreateSignature(IToken token, string verb, Uri uri, string verifier, 
            bool renewToken = false);

        protected abstract IToken RenewToken(IToken sessionToken, IConsumer consumer);

        protected IToken GetToken(IConsumer consumer)
        {
            var oauthToken = Tokens.GetRequestToken(consumer, GetAuthorization(new Token
            {
                ConsumerKey = consumer.ConsumerKey,
                ConsumerSecret = consumer.ConsumerSecret
            }, "POST", Tokens.RequestUri));

            var verifier = AuthorizeUser(oauthToken);

            return Tokens.GetAccessToken(oauthToken,
                GetAuthorization(oauthToken, "POST", Tokens.AccessUri, null, verifier));
        }

        protected string GetAuthorization(IToken token, string verb, string endpoint, string query = null, 
            string verifier = null, bool renewToken = false)
        {
            var uri = new UriBuilder(BaseUri)
            {
                Path = endpoint
            };

            if (!string.IsNullOrWhiteSpace(query))
            {
                uri.Query = query.TrimStart('?');
            }

            return CreateSignature(token, verb, uri.Uri, verifier, renewToken);
        }
    }
}
