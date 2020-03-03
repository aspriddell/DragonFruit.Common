﻿// DragonFruit.Common Copyright 2020 DragonFruit Network
// Licensed under the MIT License. Please refer to the LICENSE file at the root of this project for details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using DragonFruit.Common.API.Attributes;

namespace DragonFruit.Common.API
{
    public abstract class ApiRequest
    {
        public FormUrlEncodedContent FormContent => new FormUrlEncodedContent(GetParameter<FormParameter>());

        public string Query => "?" + string.Join("&", GetParameter<QueryParameter>()
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));

        public IEnumerable<KeyValuePair<string, string>> GetParameter<T>() where T : IProperty
        {
            foreach (var property in GetType().GetProperties())
            {
                if (Attribute.GetCustomAttribute(property, typeof(T)) is T parameter)
                {
                    var value = property.GetValue(this, null);
                    if (value != null)
                        yield return new KeyValuePair<string, string>(parameter.Name, value.ToString());
                }
            }
        }
    }
}