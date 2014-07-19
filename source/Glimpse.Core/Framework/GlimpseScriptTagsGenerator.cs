﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Glimpse.Core.Extensibility;
using Tavis.UriTemplates;

namespace Glimpse.Core.Framework
{
    /// <summary>
    /// Generator of Glimpse script tags
    /// </summary>
    public class GlimpseScriptTagsGenerator : IGlimpseScriptTagsGenerator
    {
        private IReadonlyConfiguration Configuration { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GlimpseScriptTagsGenerator" />
        /// </summary>
        /// <param name="configuration">A <see cref="IReadonlyConfiguration"/></param>
        public GlimpseScriptTagsGenerator(IReadonlyConfiguration configuration)
        {
            Guard.ArgumentNotNull("configuration", configuration);
            Configuration = configuration;
        }

        /// <summary>
        /// Generates Glimpse script tags for the given Glimpse request id
        /// </summary>
        /// <param name="glimpseRequestId">The Glimpse request Id of the request for which script tags must be generated</param>
        /// <returns>The generated script tags</returns>
        public string Generate(Guid glimpseRequestId)
        {
            var encoder = Configuration.HtmlEncoder;
            var resourceEndpoint = Configuration.ResourceEndpoint;
            var clientScripts = Configuration.ClientScripts;
            var logger = Configuration.Logger;
            var resources = Configuration.Resources;

            var stringBuilder = new StringBuilder();

            foreach (var clientScript in clientScripts.OrderBy(cs => cs.Order))
            {
                var dynamicScript = clientScript as IDynamicClientScript;
                if (dynamicScript != null)
                {
                    try
                    {
                        var requestTokenValues = new Dictionary<string, string>
                                         {
                                             { ResourceParameter.RequestId.Name, glimpseRequestId.ToString() },
                                             { ResourceParameter.VersionNumber.Name, Configuration.Version },
                                             { ResourceParameter.Hash.Name, Configuration.Hash }
                                         };

                        var resourceName = dynamicScript.GetResourceName();
                        var resource = resources.FirstOrDefault(r => r.Name.Equals(resourceName, StringComparison.InvariantCultureIgnoreCase));

                        if (resource == null)
                        {
                            logger.Warn(Resources.RenderClientScriptMissingResourceWarning, clientScript.GetType(), resourceName);
                            continue;
                        }

                        var uriTemplate = resourceEndpoint.GenerateUriTemplate(resource, Configuration.EndpointBaseUri, logger);

                        var resourceParameterProvider = dynamicScript as IParameterValueProvider;

                        if (resourceParameterProvider != null)
                        {
                            resourceParameterProvider.OverrideParameterValues(requestTokenValues);
                        }

                        var template = SetParameters(new UriTemplate(uriTemplate), requestTokenValues);
                        var uri = encoder.HtmlAttributeEncode(template.Resolve());

                        if (!string.IsNullOrEmpty(uri))
                        {
                            stringBuilder.AppendFormat(@"<script type='text/javascript' src='{0}'></script>", uri);
                        }

                        continue;
                    }
                    catch (Exception exception)
                    {
                        logger.Error(Core.Resources.GenerateScriptTagsDynamicException, exception, dynamicScript.GetType());
                    }
                }

                var staticScript = clientScript as IStaticClientScript;
                if (staticScript != null)
                {
                    try
                    {
                        var uri = encoder.HtmlAttributeEncode(staticScript.GetUri(Configuration.Version));

                        if (!string.IsNullOrEmpty(uri))
                        {
                            stringBuilder.AppendFormat(@"<script type='text/javascript' src='{0}'></script>", uri);
                        }

                        continue;
                    }
                    catch (Exception exception)
                    {
                        logger.Error(Core.Resources.GenerateScriptTagsStaticException, exception, staticScript.GetType());
                    }
                }

                logger.Warn(Core.Resources.RenderClientScriptImproperImplementationWarning, clientScript.GetType());
            }

            return stringBuilder.ToString();
        }

        private static UriTemplate SetParameters(UriTemplate template, IEnumerable<KeyValuePair<string, string>> nameValues)
        {
            if (nameValues == null)
            {
                return template;
            }

            foreach (var pair in nameValues)
            {
                template.SetParameter(pair.Key, pair.Value);
            }

            return template;
        }
    }
}