﻿// DragonFruit.Common Copyright 2020 DragonFruit Network
// Licensed under the MIT License. Please refer to the LICENSE file at the root of this project for details

using System;
using System.Collections.Generic;
using DragonFruit.Common.Data.Utils;
using SystemPath = System.IO.Path;

namespace DragonFruit.Common.Data.Basic
{
    public class BasicApiFileRequest : ApiFileRequest, IBasicApiRequest
    {
        internal override string UrlCompiler => Queries.IsValueCreated
            ? Path + QueryUtils.QueryStringFrom(Queries.Value)
            : Path;

        public BasicApiFileRequest(string path)
            : this(path, SystemPath.GetFileName(path))
        {
        }

        public BasicApiFileRequest(string path, Environment.SpecialFolder baseFolder)
            : this(path, SystemPath.Combine(Environment.GetFolderPath(baseFolder), SystemPath.GetFileName(path)))

        {
        }

        public BasicApiFileRequest(string path, string destination)
        {
            Path = path;

            if (string.IsNullOrEmpty(SystemPath.GetExtension(destination)) && !(destination.StartsWith(".") || destination.EndsWith(".")))
            {
                // we were provided with a path
                Destination = SystemPath.Combine(destination, SystemPath.GetFileName(path));
            }
            else
            {
                Destination = destination;
            }
        }

        public override string Path { get; }
        public override string Destination { get; }

        public Lazy<List<KeyValuePair<string, string>>> Queries { get; } = new Lazy<List<KeyValuePair<string, string>>>(() => new List<KeyValuePair<string, string>>());
    }
}
