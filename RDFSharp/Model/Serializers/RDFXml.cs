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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Web;

namespace RDFSharp.Model
{

    /// <summary>
    /// RDFXml is responsible for managing serialization to and from XML data format.
    /// </summary>
    internal static class RDFXml {

        #region Methods

        #region Write
        /// <summary>
        /// Serializes the given graph to the given filepath using XML data format. 
        /// </summary>
        internal static void Serialize(RDFGraph graph, String filepath) {
            Serialize(graph, new FileStream(filepath, FileMode.Create));
        }

        /// <summary>
        /// Serializes the given graph to the given stream using XML data format. 
        /// </summary>
        internal static void Serialize(RDFGraph graph, Stream outputStream) {
            try {

                #region serialize
                using (XmlTextWriter rdfxmlWriter = new XmlTextWriter(outputStream, Encoding.UTF8))  {
                    XmlDocument rdfDoc            = new XmlDocument();
                    rdfxmlWriter.Formatting       = Formatting.Indented;

                    #region xmlDecl
                    XmlDeclaration xmlDecl        = rdfDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
                    rdfDoc.AppendChild(xmlDecl);
                    #endregion

                    #region rdfRoot
                    XmlNode rdfRoot               = rdfDoc.CreateNode(XmlNodeType.Element, RDFVocabulary.RDF.PREFIX + ":RDF", RDFVocabulary.RDF.BASE_URI);
                    XmlAttribute rdfRootNS        = rdfDoc.CreateAttribute("xmlns:" + RDFVocabulary.RDF.PREFIX);
                    XmlText rdfRootNSText         = rdfDoc.CreateTextNode(RDFVocabulary.RDF.BASE_URI);
                    rdfRootNS.AppendChild(rdfRootNSText);
                    rdfRoot.Attributes.Append(rdfRootNS);

                    #region prefixes
                    //Write the graph's prefixes (except for "rdf", which has already been written)
                    graph.GraphMetadata.Namespaces.ForEach(p => {
                        if (!p.Prefix.Equals(RDFVocabulary.RDF.PREFIX, StringComparison.Ordinal) && !p.Prefix.Equals("base", StringComparison.Ordinal)) {
                            XmlAttribute pfRootNS     = rdfDoc.CreateAttribute("xmlns:" + p.Prefix);
                            XmlText pfRootNSText      = rdfDoc.CreateTextNode(p.ToString());
                            pfRootNS.AppendChild(pfRootNSText);
                            rdfRoot.Attributes.Append(pfRootNS);
                        }
                    });
                    //Write the graph's base uri to resolve eventual relative #IDs
                    XmlAttribute pfBaseNS             = rdfDoc.CreateAttribute(RDFVocabulary.XML.PREFIX + ":base");
                    XmlText pfBaseNSText              = rdfDoc.CreateTextNode(graph.Context.ToString());
                    pfBaseNS.AppendChild(pfBaseNSText);
                    rdfRoot.Attributes.Append(pfBaseNS);
                    #endregion

                    #region linq
                    //Group the graph's triples by subj
                    var groupedList =  (from    triple in graph
                                        orderby triple.Subject.ToString()
                                        group   triple by new {
                                            subj = triple.Subject.ToString()
                                        });
                    #endregion

                    #region graph
                    //Iterate over the calculated groups
                    Dictionary<RDFResource, XmlNode> containers = new Dictionary<RDFResource, XmlNode>();
                    
                    //Floating containers have reification subject which is never object of any graph's triple
                    Boolean floatingContainers                  = graph.GraphMetadata.Containers.Keys.Any(k =>
                                                                        graph.Triples.Values.Count(v => v.Object.Equals(k)) == 0);
                    //Floating collections have reification subject which is never object of any graph's triple
                    Boolean floatingCollections                 = graph.GraphMetadata.Collections.Keys.Any(k => 
                                                                        graph.Triples.Values.Count(v => v.Object.Equals(k)) == 0);

                    foreach (var group in groupedList) {

                        #region subj
                        //Check if the current subj is a container or a collection subj: if so it must be
                        //serialized in the canonical RDF/XML way instead of the "rdf:Description" way
                        XmlNode subjNode              = null;
                        String subj                   = group.Key.subj;

                        //It is a container subj, so add it to the containers pool
                        if (graph.GraphMetadata.Containers.Keys.Any(k => k.ToString().Equals(subj, StringComparison.Ordinal)) && !floatingContainers) {
                            switch (graph.GraphMetadata.Containers.Single(c => c.Key.ToString().Equals(subj, StringComparison.Ordinal)).Value) {
                                case RDFModelEnums.RDFContainerType.Bag:
                                    subjNode  = rdfDoc.CreateNode(XmlNodeType.Element, RDFVocabulary.RDF.PREFIX + ":Bag", RDFVocabulary.RDF.BASE_URI);
                                    containers.Add(new RDFResource(subj), subjNode);
                                    break;
                                case RDFModelEnums.RDFContainerType.Seq:
                                    subjNode  = rdfDoc.CreateNode(XmlNodeType.Element, RDFVocabulary.RDF.PREFIX + ":Seq", RDFVocabulary.RDF.BASE_URI);
                                    containers.Add(new RDFResource(subj), subjNode);
                                    break;
                                case RDFModelEnums.RDFContainerType.Alt:
                                    subjNode  = rdfDoc.CreateNode(XmlNodeType.Element, RDFVocabulary.RDF.PREFIX + ":Alt", RDFVocabulary.RDF.BASE_URI);
                                    containers.Add(new RDFResource(subj), subjNode);
                                    break;
                            }
                        }

                        //It is a subj of a collection of resources, so do not append triples having it as a subject
                        //because we will reconstruct the collection and append it as a whole
                        else if (graph.GraphMetadata.Collections.Keys.Any(k => k.ToString().Equals(subj, StringComparison.Ordinal))                                                         &&
                                 graph.GraphMetadata.Collections.Single(c => c.Key.ToString().Equals(subj, StringComparison.Ordinal)).Value.ItemType == RDFModelEnums.RDFItemType.Resource &&
                                 !floatingCollections) {
                            continue;
                        }

                        //It is neither a container or a collection subj
                        else {
                            subjNode                       = rdfDoc.CreateNode(XmlNodeType.Element, RDFVocabulary.RDF.PREFIX + ":Description", RDFVocabulary.RDF.BASE_URI);
                            //<rdf:Description rdf:nodeID="blankID">
                            XmlAttribute subjNodeDesc      = null;
                            XmlText subjNodeDescText       = rdfDoc.CreateTextNode(group.Key.subj);
                            if (group.Key.subj.StartsWith("bnode:")) {
                                subjNodeDescText.InnerText = subjNodeDescText.InnerText.Replace("bnode:", String.Empty);
                                subjNodeDesc               = rdfDoc.CreateAttribute(RDFVocabulary.RDF.PREFIX + ":nodeID", RDFVocabulary.RDF.BASE_URI);
                            }
                            //<rdf:Description rdf:about="subjURI">
                            else {
                                subjNodeDesc               = rdfDoc.CreateAttribute(RDFVocabulary.RDF.PREFIX + ":about", RDFVocabulary.RDF.BASE_URI);
                            }
                            subjNodeDesc.AppendChild(subjNodeDescText);
                            subjNode.Attributes.Append(subjNodeDesc);
                        }
                        #endregion

                        #region predObjList
                        //Iterate over the triples of the current group
                        foreach (var triple in group) {

                            //Do not append the triple if it is "SUBJECT rdf:type rdf:[Bag|Seq|Alt]" 
                            if (!(triple.Predicate.Equals(RDFVocabulary.RDF.TYPE) &&
                                  (subjNode.Name.Equals(RDFVocabulary.RDF.PREFIX + ":Bag", StringComparison.Ordinal) ||
                                   subjNode.Name.Equals(RDFVocabulary.RDF.PREFIX + ":Seq", StringComparison.Ordinal) ||
                                   subjNode.Name.Equals(RDFVocabulary.RDF.PREFIX + ":Alt", StringComparison.Ordinal)))) {

                                #region pred
                                String predString     = triple.Predicate.ToString();
                                //"<predPREF:predURI"
                                RDFNamespace predNS   = 
								    (RDFNamespaceRegister.GetByNamespace(predString) ?? 
									     RDFModelUtilities.GenerateNamespace(predString, false));
                                //Refine the pred with eventually necessary sanitizations
                                String predUri        = predString.Replace(predNS.ToString(), predNS.Prefix + ":")
                                                                  .Replace(":#", ":")
                                                                  .TrimEnd(new Char[] { ':', '/' });
                                //Sanitize eventually detected automatic namespace
                                if (predUri.StartsWith("autoNS:")) {
                                    predUri           = predUri.Replace("autoNS:", string.Empty);
                                }
                                //Do not write "xmlns" attribute if the predUri is the context of the graph
                                XmlNode predNode      = null;
                                if (predNS.ToString().Equals(graph.Context.ToString(), StringComparison.Ordinal)) {
                                    predNode          = rdfDoc.CreateNode(XmlNodeType.Element, predUri, null);
                                }
                                else {
                                    predNode          = rdfDoc.CreateNode(XmlNodeType.Element, predUri, predNS.ToString());
                                }
                                #endregion

                                #region object
                                if (triple.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO) {

                                    //If the object is a container subj, we must append its entire node saved in the containers dictionary
                                    if (containers.Keys.Any(k => k.Equals(triple.Object)) && !floatingContainers) {
                                        predNode.AppendChild(containers.Single(c => c.Key.Equals(triple.Object)).Value);
                                    }

                                    //Else, if the object is a subject of a collection of resources, we must append the "rdf:parseType=Collection" attribute to the predicate node
                                    else if (graph.GraphMetadata.Collections.Keys.Any(k => k.Equals(triple.Object))                                                                                     &&
                                             graph.GraphMetadata.Collections.Single(c => c.Key.Equals(triple.Object)).Value.ItemType == RDFModelEnums.RDFItemType.Resource &&
                                             !floatingCollections) {
                                        XmlAttribute rdfParseType  = rdfDoc.CreateAttribute(RDFVocabulary.RDF.PREFIX + ":parseType", RDFVocabulary.RDF.BASE_URI);
                                        XmlText rdfParseTypeText   = rdfDoc.CreateTextNode("Collection");
                                        rdfParseType.AppendChild(rdfParseTypeText);
                                        predNode.Attributes.Append(rdfParseType);
                                        //Then we append sequentially the collection elements 
                                        List<XmlNode> collElements = ReconstructCollection(graph.GraphMetadata, (RDFResource)triple.Object, rdfDoc);
                                        collElements.ForEach(c => predNode.AppendChild(c)); 
                                    }

                                    //Else, threat it as a traditional object node
                                    else {
                                        String objString               = triple.Object.ToString();
                                        XmlAttribute predNodeDesc      = null;
                                        XmlText predNodeDescText       = rdfDoc.CreateTextNode(objString);
                                        //  rdf:nodeID="blankID">
                                        if (objString.StartsWith("bnode:")) {
                                            predNodeDescText.InnerText = predNodeDescText.InnerText.Replace("bnode:", String.Empty);  
                                            predNodeDesc               = rdfDoc.CreateAttribute(RDFVocabulary.RDF.PREFIX + ":nodeID", RDFVocabulary.RDF.BASE_URI);
                                        }
                                        //  rdf:resource="objURI">
                                        else {
                                            predNodeDesc               = rdfDoc.CreateAttribute(RDFVocabulary.RDF.PREFIX + ":resource", RDFVocabulary.RDF.BASE_URI);
                                        }
                                        predNodeDesc.AppendChild(predNodeDescText);
                                        predNode.Attributes.Append(predNodeDesc);
                                    }
                                }
                                #endregion

                                #region literal
                                else {

                                    #region plain literal
                                    if (triple.Object is RDFPlainLiteral) {
                                        RDFPlainLiteral pLit      = (RDFPlainLiteral)triple.Object;
                                        //  xml:lang="plitLANG">
                                        if (pLit.Language        != String.Empty) {
                                            XmlAttribute plainLiteralLangNodeDesc = rdfDoc.CreateAttribute(RDFVocabulary.XML.PREFIX + ":lang", RDFVocabulary.XML.BASE_URI);
                                            XmlText plainLiteralLangNodeDescText  = rdfDoc.CreateTextNode(pLit.Language);
                                            plainLiteralLangNodeDesc.AppendChild(plainLiteralLangNodeDescText);
                                            predNode.Attributes.Append(plainLiteralLangNodeDesc);
                                        }
                                    }
                                    #endregion

                                    #region typed literal
                                    //  rdf:datatype="tlitURI">
                                    else {
                                        RDFTypedLiteral tLit      = (RDFTypedLiteral)triple.Object;
                                        XmlAttribute typedLiteralNodeDesc = rdfDoc.CreateAttribute(RDFVocabulary.RDF.PREFIX + ":datatype", RDFVocabulary.RDF.BASE_URI);
                                        XmlText typedLiteralNodeDescText  = rdfDoc.CreateTextNode(RDFModelUtilities.GetDatatypeFromEnum(tLit.Datatype));
                                        typedLiteralNodeDesc.AppendChild(typedLiteralNodeDescText);
                                        predNode.Attributes.Append(typedLiteralNodeDesc);
                                    }
                                    #endregion

                                    //litVALUE</predPREF:predURI>"
                                    XmlText litNodeDescText       = rdfDoc.CreateTextNode(((RDFLiteral)triple.Object).Value);
                                    predNode.AppendChild(litNodeDescText);
                                }
                                #endregion

                                subjNode.AppendChild(predNode);
                            }

                        }

                        //Raw containers must not be written as-is, instead they have to be saved
                        //and attached when their subj is found later as object of a triple
                        if (!subjNode.Name.Equals(RDFVocabulary.RDF.PREFIX + ":Bag", StringComparison.Ordinal) &&
                            !subjNode.Name.Equals(RDFVocabulary.RDF.PREFIX + ":Seq", StringComparison.Ordinal) &&
                            !subjNode.Name.Equals(RDFVocabulary.RDF.PREFIX + ":Alt", StringComparison.Ordinal)) {
                             rdfRoot.AppendChild(subjNode);
                        }
                        #endregion

                    }
                    #endregion

                    rdfDoc.AppendChild(rdfRoot);
                    #endregion

                    rdfDoc.Save(rdfxmlWriter);
                }
                #endregion

            }
            catch (Exception ex) {
                throw new RDFModelException("Cannot serialize Xml because: " + ex.Message, ex);
            }
        }
        #endregion

        #region Read
        /// <summary>
        /// Deserializes the given Xml filepath to a graph. 
        /// </summary>
        internal static RDFGraph Deserialize(String filepath) {
            return Deserialize(new FileStream(filepath, FileMode.Open));
        }

        /// <summary>
        /// Deserializes the given Xml stream to a graph. 
        /// </summary>
        internal static RDFGraph Deserialize(Stream inputStream) {
            try {

                #region deserialize
                XmlReaderSettings xrs    = new XmlReaderSettings();
                xrs.IgnoreComments       = true;
                xrs.DtdProcessing        = DtdProcessing.Ignore;

                RDFGraph result          = new RDFGraph();
                using(XmlReader xr       = XmlReader.Create(new StreamReader(inputStream, Encoding.UTF8), xrs)) {

                    #region load
                    XmlDocument xmlDoc   = new XmlDocument();
                    xmlDoc.Load(xr);
                    #endregion

                    #region root
                    //Prepare the namespace table for the Xml selections
                    var nsMgr            = new XmlNamespaceManager(new NameTable());
                    nsMgr.AddNamespace(RDFVocabulary.RDF.PREFIX, RDFVocabulary.RDF.BASE_URI);

                    //Select "rdf:RDF" root node
                    XmlNode rdfRDF       = GetRdfRootNode(xmlDoc, nsMgr);
                    #endregion

                    #region prefixes
                    //Select "xmlns" attributes and try to add them to the namespace register
                    var xmlnsAttrs       = GetXmlnsNamespaces(rdfRDF, nsMgr);

                    //Try to get the "xml:base" attribute, which is needed to resolve eventual relative #IDs in "rdf:about" nodes
                    //If it is not found, set it to the graph Uri
                    Uri xmlBase          = null;
                    if (xmlnsAttrs      != null && xmlnsAttrs.Count > 0) {
                        var xmlBaseAttr  = (rdfRDF.Attributes["xml:base"] ?? rdfRDF.Attributes["xmlns"]);
                        if (xmlBaseAttr != null) {
                            xmlBase      = RDFModelUtilities.GetUriFromString(xmlBaseAttr.Value);
                        }
                    }
                    //Always keep in synch the Context and the xmlBase
                    if(xmlBase          != null) {
                        result.SetContext(xmlBase);
                    }
                    else {
                        xmlBase          = result.Context;
                    }
                    #endregion

                    #region elements
                    //Parse resource elements, which are the childs of root node and represent the subjects
                    if (rdfRDF.HasChildNodes) {
                        var subjNodesEnum     = rdfRDF.ChildNodes.GetEnumerator();
                        while(subjNodesEnum  != null && subjNodesEnum.MoveNext()) {

                            #region subj
                            //Get the current resource node
                            XmlNode subjNode  = (XmlNode)subjNodesEnum.Current;
                            RDFResource subj  = GetSubjectNode(subjNode, xmlBase, result);
                            if(subj          == null) {
                                continue;
                            }
                            #endregion

                            #region predObjList
                            //Parse pred elements, which are the childs of subj element
                            if (subjNode.HasChildNodes) {
                                IEnumerator predNodesEnum     = subjNode.ChildNodes.GetEnumerator();
                                while(predNodesEnum          != null && predNodesEnum.MoveNext()) {

                                    //Get the current pred node
                                    RDFResource pred          = null;
                                    XmlNode predNode          = (XmlNode)predNodesEnum.Current;
                                    if(predNode.NamespaceURI == String.Empty) {
                                       pred                   = new RDFResource(xmlBase + predNode.LocalName);
                                    }
                                    else {
                                        pred                  = (predNode.LocalName.StartsWith("autoNS") ?
                                                                    new RDFResource(predNode.NamespaceURI) :
                                                                    new RDFResource(predNode.NamespaceURI + predNode.LocalName));
                                    }

                                    #region object
                                    //Check if there is a "rdf:about" or a "rdf:resource" attribute
                                    XmlAttribute rdfObject    =
                                        (GetRdfAboutAttribute(predNode) ??
                                            GetRdfResourceAttribute(predNode));
                                    if(rdfObject             != null) {
                                        //Attribute found, but we must check if it is "rdf:ID", "rdf:nodeID" or a relative Uri
                                        String rdfObjectValue = ResolveRelativeNode(rdfObject, xmlBase);
                                        RDFResource  obj      = new RDFResource(rdfObjectValue);
                                        result.AddTriple(new RDFTriple(subj, pred, obj));
                                        continue;
                                    }
                                    #endregion

                                    #region typed literal
                                    //Check if there is a "rdf:datatype" attribute
                                    XmlAttribute rdfDatatype         = GetRdfDatatypeAttribute(predNode);
                                    if (rdfDatatype                 != null) {
                                        RDFModelEnums.RDFDatatype dt = RDFModelUtilities.GetDatatypeFromString(rdfDatatype.Value);
                                        RDFTypedLiteral tLit         = new RDFTypedLiteral(HttpUtility.HtmlDecode(predNode.InnerText), dt);
                                        result.AddTriple(new RDFTriple(subj, pred, tLit));
                                        continue;
                                    }
                                    //Check if there is a "rdf:parseType=Literal" attribute
                                    XmlAttribute parseLiteral = GetParseTypeLiteralAttribute(predNode);
                                    if (parseLiteral         != null) {
                                        RDFTypedLiteral tLit  = new RDFTypedLiteral(HttpUtility.HtmlDecode(predNode.InnerXml), RDFModelEnums.RDFDatatype.RDFS_LITERAL);
                                        result.AddTriple(new RDFTriple(subj, pred, tLit));
                                        continue;
                                    }
                                    #endregion

                                    #region plain literal
                                    //Check if there is a "xml:lang" attribute, or if a unique textual child
                                    XmlAttribute xmlLang      = GetXmlLangAttribute(predNode);
                                    if (xmlLang              != null || (predNode.HasChildNodes && predNode.ChildNodes.Count == 1 && predNode.ChildNodes[0].NodeType == XmlNodeType.Text)) {
                                        RDFPlainLiteral pLit  = new RDFPlainLiteral(HttpUtility.HtmlDecode(predNode.InnerText), (xmlLang != null ? xmlLang.Value : String.Empty));
                                        result.AddTriple(new RDFTriple(subj, pred, pLit));
                                        continue;
                                    }
                                    #endregion

                                    #region collection
                                    //Check if there is a "rdf:parseType=Collection" attribute
                                    XmlAttribute rdfCollect   = GetParseTypeCollectionAttribute(predNode);
                                    if (rdfCollect           != null) {
                                        ParseCollectionElements(xmlBase, predNode, subj, pred, result);
                                        continue;
                                    }
                                    #endregion

                                    #region container
                                    //Check if there is a "rdf:[Bag|Seq|Alt]" child node
                                    XmlNode container        = GetContainerNode(predNode);
                                    if (container           != null) {
                                        //Distinguish the right type of RDF container to build
                                        if (container.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":Bag", StringComparison.Ordinal)     || container.LocalName.Equals("Bag", StringComparison.Ordinal)) {
                                            ParseContainerElements(RDFModelEnums.RDFContainerType.Bag, container, subj, pred, result);
                                        }
                                        else if(container.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":Seq", StringComparison.Ordinal) || container.LocalName.Equals("Seq", StringComparison.Ordinal)) {
                                            ParseContainerElements(RDFModelEnums.RDFContainerType.Seq, container, subj, pred, result);
                                        }
                                        else if(container.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":Alt", StringComparison.Ordinal) || container.LocalName.Equals("Alt", StringComparison.Ordinal)) {
                                            ParseContainerElements(RDFModelEnums.RDFContainerType.Alt, container, subj, pred, result);
                                        }
                                    }
                                    #endregion

                                }
                            }
                            #endregion

                        }
                    }
                    #endregion

                }
                return result;
                #endregion

            }
            catch(Exception ex) {
                throw new RDFModelException("Cannot deserialize Xml because: " + ex.Message, ex);
            }
        }
        #endregion

        #region Utilities
        /// <summary>
        /// Gives the "rdf:RDF" root node of the document
        /// </summary>
        internal static XmlNode GetRdfRootNode(XmlDocument xmlDoc, XmlNamespaceManager nsMgr) {
            XmlNode rdf =
                (xmlDoc.SelectSingleNode(RDFVocabulary.RDF.PREFIX + ":RDF", nsMgr) ??
                    xmlDoc.SelectSingleNode("RDF", nsMgr));

            //Invalid RDF/XML file: root node is neither "rdf:RDF" or "RDF"
            if (rdf == null) {
                throw new Exception("Given file has not a valid \"rdf:RDF\" or \"RDF\" root node");
            }

            return rdf;
        }

        /// <summary>
        /// Gives the collection of "xmlns" attributes of the "rdf:RDF" root node
        /// </summary>
        internal static XmlAttributeCollection GetXmlnsNamespaces(XmlNode rdfRDF, XmlNamespaceManager nsMgr) {
            XmlAttributeCollection xmlns = rdfRDF.Attributes;
            if (xmlns != null && xmlns.Count > 0) {

                IEnumerator iEnum        = xmlns.GetEnumerator();
                while (iEnum            != null && iEnum.MoveNext()) {
                    XmlAttribute attr    = (XmlAttribute)iEnum.Current;
                    if (attr.LocalName.ToUpperInvariant() != "XMLNS") {

                        //Try to resolve the current namespace against the namespace register; 
                        //if not resolved, create new namespace with scope limited to actual node
                        RDFNamespace ns  =
                        (RDFNamespaceRegister.GetByPrefix(attr.LocalName) ??
                                RDFNamespaceRegister.GetByNamespace(attr.Value) ??
                                    new RDFNamespace(attr.LocalName, attr.Value));

                        nsMgr.AddNamespace(ns.Prefix, ns.Namespace.ToString());

                    }
                }

            }
            return xmlns;
        }

        /// <summary>
        /// Gives the subj node extracted from the attribute list of the current element 
        /// </summary>
        internal static RDFResource GetSubjectNode(XmlNode subjNode, Uri xmlBase, RDFGraph result) {
            RDFResource subj             = null;

            //If there are attributes, search them for the one representing the subj
            if (subjNode.Attributes     != null && subjNode.Attributes.Count > 0) {

                //We are interested in finding the "rdf:about" node for the subj
                XmlAttribute rdfAbout    = GetRdfAboutAttribute(subjNode);
                if (rdfAbout            != null) {
                    //Attribute found, but we must check if it is "rdf:ID", "rdf:nodeID" or a relative Uri: 
                    //in this case it must be resolved against the xmlBase namespace, or else it remains the same
                    String rdfAboutValue = ResolveRelativeNode(rdfAbout, xmlBase);
                    subj                 = new RDFResource(rdfAboutValue);
                }

                //If "rdf:about" attribute has been found for the subj, we must
                //check if the node is not a standard "rdf:Description": this is
                //the case we can directly build a triple with "rdf:type" pred
                if (subj                != null && !CheckIfRdfDescriptionNode(subjNode)) {
                    RDFResource obj      = null;
                    if (subjNode.NamespaceURI == String.Empty) {
                        obj              = new RDFResource(xmlBase + subjNode.LocalName);
                    }
                    else {
                        obj              = new RDFResource(subjNode.NamespaceURI + subjNode.LocalName);
                    }
                    result.AddTriple(new RDFTriple(subj, RDFVocabulary.RDF.TYPE, obj));
                }

            }

            //There are no attributes, so there's only one way we can handle this element:
            //if it is a standard rdf:Description, it is a blank Subject
            else {
                if (CheckIfRdfDescriptionNode(subjNode)) {
                    subj                 = new RDFResource();
                }
            }

            return subj;
        }

        /// <summary>
        /// Checks if the given attribute is absolute Uri, relative Uri, "rdf:ID" relative Uri, "rdf:nodeID" blank node Uri
        /// </summary>
        internal static String ResolveRelativeNode(XmlAttribute attr, Uri xmlBase) {
            if (attr               != null && xmlBase != null) {
                String attrValue    = attr.Value;

                //"rdf:ID" relative Uri: must be resolved against the xmlBase namespace
                if (attr.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":ID", StringComparison.Ordinal) ||
                    attr.LocalName.Equals("ID", StringComparison.Ordinal)) {
                    attrValue       = RDFModelUtilities.GetUriFromString(xmlBase + attrValue).ToString();
                }

                //"rdf:nodeID" relative Uri: must be resolved against the "bnode:" prefix
                else if (attr.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":nodeID", StringComparison.Ordinal) ||
                         attr.LocalName.Equals("nodeID", StringComparison.Ordinal)) {
                    if (!attrValue.StartsWith("bnode:")) {
                         attrValue  = "bnode:" + attrValue;
                    }
                }

                //"rdf:about" or "rdf:resource" relative Uri: must be resolved against the xmlBase namespace
                else if (RDFModelUtilities.GetUriFromString(attrValue) == null) {
                    attrValue       = RDFModelUtilities.GetUriFromString(xmlBase + attrValue).ToString();
                }

                return attrValue;
            }
            throw new RDFModelException("Cannot resolve relative node because given \"attr\" or \"xmlBase\" parameters are null");
        }

        /// <summary>
        /// Verify if we are on a standard rdf:Description element
        /// </summary>
        internal static Boolean CheckIfRdfDescriptionNode(XmlNode subjNode) {
            Boolean result = (subjNode.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":Description", StringComparison.Ordinal) ||
                              subjNode.LocalName.Equals("Description", StringComparison.Ordinal));

            return result;
        }

        /// <summary>
        /// Given an element, return the attribute which can correspond to the RDF subj
        /// </summary>
        internal static XmlAttribute GetRdfAboutAttribute(XmlNode subjNode) {
            //rdf:about
            XmlAttribute rdfAbout =
                (subjNode.Attributes[RDFVocabulary.RDF.PREFIX + ":about"] ??
                    (subjNode.Attributes["about"] ??
                        (subjNode.Attributes[RDFVocabulary.RDF.PREFIX + ":nodeID"] ??
                            (subjNode.Attributes["nodeID"] ??
                                (subjNode.Attributes[RDFVocabulary.RDF.PREFIX + ":ID"] ??
                                    subjNode.Attributes["ID"])))));

            return rdfAbout;
        }

        /// <summary>
        /// Given an element, return the attribute which can correspond to the RDF object
        /// </summary>
        internal static XmlAttribute GetRdfResourceAttribute(XmlNode predNode) {
            //rdf:Resource
            XmlAttribute rdfResource =
                (predNode.Attributes[RDFVocabulary.RDF.PREFIX + ":resource"] ??
                    (predNode.Attributes["resource"] ??
                        (predNode.Attributes[RDFVocabulary.RDF.PREFIX + ":nodeID"] ??
                            predNode.Attributes["nodeID"])));

            return rdfResource;
        }

        /// <summary>
        /// Given an element, return the attribute which can correspond to the RDF typed literal datatype
        /// </summary>
        internal static XmlAttribute GetRdfDatatypeAttribute(XmlNode predNode) {
            //rdf:datatype
            XmlAttribute rdfDatatype =
                (predNode.Attributes[RDFVocabulary.RDF.PREFIX + ":datatype"] ??
                    predNode.Attributes["datatype"]);

            return rdfDatatype;
        }

        /// <summary>
        /// Given an element, return the attribute which can correspond to the RDF plain literal language
        /// </summary>
        internal static XmlAttribute GetXmlLangAttribute(XmlNode predNode) {
            //xml:lang
            XmlAttribute xmlLang =
                (predNode.Attributes[RDFVocabulary.XML.PREFIX + ":lang"] ??
                    predNode.Attributes["lang"]);

            return xmlLang;
        }

        /// <summary>
        /// Given an element, return the attribute which can correspond to the RDF parseType "Collection"
        /// </summary>
        internal static XmlAttribute GetParseTypeCollectionAttribute(XmlNode predNode) {
            XmlAttribute rdfCollection =
                (predNode.Attributes[RDFVocabulary.RDF.PREFIX + ":parseType"] ??
                    predNode.Attributes["parseType"]);

            return ((rdfCollection != null && rdfCollection.Value.Equals("Collection", StringComparison.Ordinal)) ? rdfCollection : null);
        }

        /// <summary>
        /// Given an element, return the attribute which can correspond to the RDF parseType "Literal"
        /// </summary>
        internal static XmlAttribute GetParseTypeLiteralAttribute(XmlNode predNode) {
            XmlAttribute rdfLiteral =
                (predNode.Attributes[RDFVocabulary.RDF.PREFIX + ":parseType"] ??
                    predNode.Attributes["parseType"]);

            return ((rdfLiteral    != null && rdfLiteral.Value.Equals("Literal", StringComparison.Ordinal)) ? rdfLiteral : null);
        }

        /// <summary>
        /// Given an attribute representing a RDF collection, iterates on its constituent elements
        /// to build its standard reification triples. 
        /// </summary>
        internal static void ParseCollectionElements(Uri xmlBase, XmlNode predNode, RDFResource subj,
                                                     RDFResource pred, RDFGraph result) {

            //Attach the collection as the blank object of the current pred
            RDFResource  obj              = new RDFResource();
            result.AddTriple(new RDFTriple(subj, pred, obj));

            //Iterate on the collection items to reify it
            if (predNode.HasChildNodes) {
                IEnumerator elems         = predNode.ChildNodes.GetEnumerator();
                while (elems             != null && elems.MoveNext()) {
                    XmlNode elem          = (XmlNode)elems.Current;

                    //Try to get items as "rdf:about" attributes, or as "rdf:resource"
                    XmlAttribute elemUri  =
                        (GetRdfAboutAttribute(elem) ??
                             GetRdfResourceAttribute(elem));
                    if (elemUri          != null) {

                        //Sanitize eventual blank node or relative value, depending on attribute found
                        elemUri.Value     = ResolveRelativeNode(elemUri, xmlBase);

                        // obj -> rdf:type -> rdf:list
                        result.AddTriple(new RDFTriple(obj, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.LIST));

                        // obj -> rdf:first -> res
                        result.AddTriple(new RDFTriple(obj, RDFVocabulary.RDF.FIRST, new RDFResource(elemUri.Value)));

                        //Last element of a collection must give a triple to a "rdf:nil" object
                        RDFResource newObj;
                        if (elem         != predNode.ChildNodes.Item(predNode.ChildNodes.Count - 1)) {
                            // obj -> rdf:rest -> newObj
                            newObj        = new RDFResource();
                        }
                        else {
                            // obj -> rdf:rest -> rdf:nil
                            newObj        = RDFVocabulary.RDF.NIL;
                        }
                        result.AddTriple(new RDFTriple(obj, RDFVocabulary.RDF.REST, newObj));
                        obj               = newObj;

                    }
                }
            }
        }

        /// <summary>
        /// Given the metadata of a graph and a collection resource, it reconstructs the RDF collection and returns it as a list of nodes
        /// This is needed for building the " rdf:parseType=Collection>" RDF/XML abbreviation goody for collections of resources
        /// </summary>
        internal static List<XmlNode> ReconstructCollection(RDFGraphMetadata rdfGraphMetadata, RDFResource tripleObject, XmlDocument rdfDoc) {
            List<XmlNode> result          = new List<XmlNode>();
            Boolean nilFound              = false;
            XmlNode collElementToAppend   = null;
            XmlAttribute collElementAttr  = null;
            XmlText collElementAttrText   = null;

            //Iterate the elements of the collection until the last one (pointing to next="rdf:nil")
            while (!nilFound) {
                var collElement           = rdfGraphMetadata.Collections[tripleObject];
                collElementToAppend       = rdfDoc.CreateNode(XmlNodeType.Element, RDFVocabulary.RDF.PREFIX + ":Description", RDFVocabulary.RDF.BASE_URI);
                collElementAttr           = rdfDoc.CreateAttribute(RDFVocabulary.RDF.PREFIX + ":about", RDFVocabulary.RDF.BASE_URI);
                collElementAttrText       = rdfDoc.CreateTextNode(collElement.ItemValue.ToString());
                if (collElementAttrText.InnerText.StartsWith("bnode:")) {
                    collElementAttrText.InnerText = collElementAttrText.InnerText.Replace("bnode:", String.Empty);
                }
                collElementAttr.AppendChild(collElementAttrText);
                collElementToAppend.Attributes.Append(collElementAttr);
                result.Add(collElementToAppend);

                //Verify if this is the last element of the collection (pointing to next="rdf:nil")
                if (collElement.ItemNext.ToString().Equals(RDFVocabulary.RDF.NIL.ToString(), StringComparison.Ordinal)) {
                    nilFound              = true;
                }
                else {
                    tripleObject          = collElement.ItemNext as RDFResource;
                }

            }

            return result;
        }

        /// <summary>
        /// Verify if we are on a standard rdf:[Bag|Seq|Alt] element
        /// </summary>
        internal static Boolean CheckIfRdfContainerNode(XmlNode containerNode) {
            Boolean result = (containerNode.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":Bag", StringComparison.Ordinal) || containerNode.LocalName.Equals("Bag", StringComparison.Ordinal) ||
                              containerNode.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":Seq", StringComparison.Ordinal) || containerNode.LocalName.Equals("Seq", StringComparison.Ordinal) ||
                              containerNode.LocalName.Equals(RDFVocabulary.RDF.PREFIX + ":Alt", StringComparison.Ordinal) || containerNode.LocalName.Equals("Alt", StringComparison.Ordinal));

            return result;
        }

        /// <summary>
        /// Given an element, return the child element which can correspond to the RDF container
        /// </summary>
        internal static XmlNode GetContainerNode(XmlNode predNode) {
            //A container is the first child of the given node and it has no attributes.
            //Its localname must be the canonical "rdf:[Bag|Seq|Alt]", so we check for this.
            if (predNode.HasChildNodes) {
                XmlNode containerNode  = predNode.FirstChild;
                Boolean isRdfContainer = CheckIfRdfContainerNode(containerNode);
                if (isRdfContainer) {
                    return containerNode;
                }
            }

            return null;
        }

        /// <summary>
        /// Given an element representing a RDF container, iterates on its constituent elements
        /// to build its standard reification triples. 
        /// </summary>
        internal static void ParseContainerElements(RDFModelEnums.RDFContainerType contType, XmlNode container,
                                                    RDFResource subj, RDFResource pred, RDFGraph result) {

            //Attach the container as the blank object of the current pred
            RDFResource  obj                 = new RDFResource();
            result.AddTriple(new RDFTriple(subj, pred, obj));

            //obj -> rdf:type -> rdf:[Bag|Seq|Alt]
            switch(contType) {
                case RDFModelEnums.RDFContainerType.Bag:
                    result.AddTriple(new RDFTriple(obj, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.BAG));
                    break;
                case RDFModelEnums.RDFContainerType.Seq:
                    result.AddTriple(new RDFTriple(obj, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.SEQ));
                    break;
                default:
                    result.AddTriple(new RDFTriple(obj, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.ALT));
                    break;
            }

            //Iterate on the container items
            if (container.HasChildNodes) {
                IEnumerator elems              = container.ChildNodes.GetEnumerator();
                List<String> elemVals          = new List<String>();
                while (elems                  != null && elems.MoveNext()) {
                    XmlNode elem               = (XmlNode)elems.Current;
                    XmlAttribute elemUri       = GetRdfResourceAttribute(elem);

                    #region Container Resource Item
                    //This is a container of resources
                    if (elemUri               != null) {

                        //Sanitize eventual blank node value detected by presence of "nodeID" attribute
                        if (elemUri.LocalName.Equals("nodeID", StringComparison.Ordinal)) {
                            if (!elemUri.Value.StartsWith("bnode:")) {
                                 elemUri.Value = "bnode:" + elemUri.Value;
                            }
                        }

                        //obj -> rdf:_N -> VALUE 
                        if (contType          == RDFModelEnums.RDFContainerType.Alt) {
                            if (!elemVals.Contains(elemUri.Value)) {
                                 elemVals.Add(elemUri.Value);
                                 result.AddTriple(new RDFTriple(obj, new RDFResource(RDFVocabulary.RDF.BASE_URI + elem.LocalName), new RDFResource(elemUri.Value)));
                            }
                        }
                        else {
                            result.AddTriple(new RDFTriple(obj, new RDFResource(RDFVocabulary.RDF.BASE_URI + elem.LocalName), new RDFResource(elemUri.Value)));
                        }

                    }
                    #endregion

                    #region Container Literal Item
                    //This is a container of literals
                    else {

                        //Parse the literal contained in the item
                        RDFLiteral literal     = null;
                        XmlAttribute attr      = GetRdfDatatypeAttribute(elem);
                        if (attr              != null) {
                            literal            = new RDFTypedLiteral(elem.InnerText, RDFModelUtilities.GetDatatypeFromString(attr.InnerText));
                        }
                        else {
                            attr               = GetXmlLangAttribute(elem);
                            literal            = new RDFPlainLiteral(elem.InnerText, (attr != null ? attr.InnerText : String.Empty));
                        }

                        //obj -> rdf:_N -> VALUE 
                        if (contType          == RDFModelEnums.RDFContainerType.Alt) {
                            if (!elemVals.Contains(literal.ToString())) {
                                 elemVals.Add(literal.ToString());
                                 result.AddTriple(new RDFTriple(obj, new RDFResource(RDFVocabulary.RDF.BASE_URI + elem.LocalName), literal));
                            }
                        }
                        else {
                            result.AddTriple(new RDFTriple(obj, new RDFResource(RDFVocabulary.RDF.BASE_URI + elem.LocalName), literal));
                        }

                    }
                    #endregion

                }
            }

        }
        #endregion

        #endregion

    }

}