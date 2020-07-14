﻿// DragonFruit.Common Copyright 2020 DragonFruit Network
// Licensed under the MIT License. Please refer to the LICENSE file at the root of this project for details

using System.Diagnostics;
using DragonFruit.Common.Data.Tests.Handlers.AuthPreservingHandler.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DragonFruit.Common.Data.Tests.Handlers.AuthPreservingHandler
{
    [TestClass]
    public class HeaderPreservingHeaderTests
    {
        [TestMethod]
        public void TestHeaderPreservation()
        {
            var redirectClient = new HeaderPreservingHandlerClient();

            //get auth token
            var request = new AuthRequest();

            //for some reason inconclusive assertion returns a fail, so we'll just "pass" this with a nice message for now
            if (request.ClientSecret == null)
            {
                Debug.WriteLine("Environment Variables not found, skipping test.");
                return;
            }

            var auth = redirectClient.Perform<BasicOrbitAuthResponse>(request);
            redirectClient.Authorization = $"{auth.Type} {auth.AccessToken}";

            //user lookups by username = 301. without our HeaderPreservingHandler we'd get a 401
            redirectClient.Perform(new OrbitTestUserRequest());
        }
    }
}
