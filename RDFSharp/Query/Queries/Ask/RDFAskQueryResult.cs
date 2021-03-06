﻿/*
   Copyright 2012-2016 Marco De Salvo

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.IO;
using System.Text;
using System.Xml;

namespace RDFSharp.Query
{

    /// <summary>
    /// RDFAskResult is a container for SPARQL "ASK" query results.
    /// </summary>
    public class RDFAskQueryResult {

        #region Properties
        /// <summary>
        /// Boolean response of the ASK query
        /// </summary>
        public Boolean AskResult { get; internal set; }
        #endregion

        #region Ctors
        /// <summary>
        /// Default-ctor to build an empty ASK result
        /// </summary>
        internal RDFAskQueryResult() {
            this.AskResult = false;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gives the "SPARQL Query Results XML Format" representation of the RDF query result into the given XML file
        /// </summary>
        public void ToSparqlXmlResult(String filepath) {
            if (filepath != null && filepath.Trim() != String.Empty) {

                #region serialize
                using (XmlTextWriter sparqlWriter = new XmlTextWriter(filepath, Encoding.UTF8)) {
                    XmlDocument sparqlDoc         = new XmlDocument();
                    sparqlWriter.Formatting       = Formatting.Indented;

                    #region xmlDecl
                    XmlDeclaration sparqlDecl     = sparqlDoc.CreateXmlDeclaration("1.0", null, null);
                    sparqlDoc.AppendChild(sparqlDecl);
                    #endregion

                    #region sparqlRoot
                    XmlNode sparqlRoot            = sparqlDoc.CreateNode(XmlNodeType.Element, "sparql", null);
                    XmlAttribute sparqlRootNS     = sparqlDoc.CreateAttribute("xmlns");
                    XmlText sparqlRootNSText      = sparqlDoc.CreateTextNode("http://www.w3.org/2005/sparql-results#");
                    sparqlRootNS.AppendChild(sparqlRootNSText);
                    sparqlRoot.Attributes.Append(sparqlRootNS);

                    #region sparqlHead
                    XmlNode sparqlHeadElement     = sparqlDoc.CreateNode(XmlNodeType.Element, "head", null);
                    sparqlRoot.AppendChild(sparqlHeadElement);
                    #endregion

                    #region sparqlResults
                    XmlNode sparqlResultsElement  = sparqlDoc.CreateNode(XmlNodeType.Element, "boolean", null);
                    XmlText askResultText         = sparqlDoc.CreateTextNode(this.AskResult.ToString().ToUpperInvariant());
                    sparqlResultsElement.AppendChild(askResultText);
                    sparqlRoot.AppendChild(sparqlResultsElement);
                    #endregion

                    sparqlDoc.AppendChild(sparqlRoot);
                    #endregion

                    sparqlDoc.Save(sparqlWriter);
                }
                #endregion

            }
        }

        /// <summary>
        /// Reads the given "SPARQL Query Results XML Format" file into a query result
        /// </summary>
        public static RDFAskQueryResult FromSparqlXmlResult(String filepath) {
            try {

                #region deserialize
                XmlReaderSettings xrs         = new XmlReaderSettings();
                xrs.IgnoreComments            = true;
                xrs.DtdProcessing             = DtdProcessing.Ignore;

                RDFAskQueryResult result      = new RDFAskQueryResult();
                using (XmlReader xr           = XmlReader.Create(new StreamReader(filepath), xrs)) {

                    #region load
                    XmlDocument srxDoc     = new XmlDocument();
                    srxDoc.Load(xr);
                    #endregion

                    #region parse
                    Boolean foundHead         = false;
                    Boolean foundBoolean      = false;
                    var nodesEnum             = srxDoc.DocumentElement.ChildNodes.GetEnumerator();
                    while (nodesEnum != null && nodesEnum.MoveNext()) {
                        XmlNode node          = (XmlNode)nodesEnum.Current;

                        #region HEAD
                        if (node.Name.ToUpperInvariant().Equals("HEAD", StringComparison.Ordinal)) {
                            foundHead         = true;
                        }
                        #endregion

                        #region BOOLEAN
                        else if (node.Name.ToUpperInvariant().Equals("BOOLEAN", StringComparison.Ordinal)) {
                            foundBoolean      = true;
                            if(foundHead) {
                                Boolean bRes  = false;
                                if (Boolean.TryParse(node.InnerText, out bRes)) {
                                    result.AskResult = bRes;
                                }
                                else {
                                    throw new Exception("\"boolean\" node contained data not corresponding to a valid Boolean.");
                                }
                            }
                            else {
                                throw new Exception("\"head\" node was not found, or was after \"boolean\" node.");
                            }
                        }
                        #endregion

                    }

                    if (!foundHead) {
                        throw new Exception("mandatory \"head\" node was not found");
                    }
                    if (!foundBoolean) {
                        throw new Exception("mandatory \"boolean\" node was not found");
                    }                        
                    #endregion

                }
                return result;
                #endregion

            }
            catch (Exception ex) {
                throw new RDFQueryException("Cannot read given \"SPARQL Query Results XML Format\" file because: " + ex.Message, ex);
            }
        }
        #endregion

    }

}