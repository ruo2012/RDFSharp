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
using System.Collections.Generic;

namespace RDFSharp.Model
{

    /// <summary>
    /// RDFGraphMetadata represents a collector for metadata describing contents of a RDFGraph.
    /// </summary>
    internal class RDFGraphMetadata {

        #region Properties
        /// <summary>
        /// List of registered namespaces used by the graph
        /// </summary>
        internal List<RDFNamespace> Namespaces { get; set; }

        /// <summary>
        /// Dictionary of resources acting as container subjects in the graph
        /// </summary>
        internal Dictionary<RDFResource, RDFModelEnums.RDFContainerType> Containers { get; set; }

        /// <summary>
        /// Dictionary of resources acting as collection subjects in the graph
        /// </summary>
        internal Dictionary<RDFResource, RDFCollectionItem> Collections { get; set; }
        #endregion

        #region Ctors
        /// <summary>
        /// Default ctor to build an empty metadata
        /// </summary>
        internal RDFGraphMetadata() {
            this.Namespaces  = new List<RDFNamespace>();
            this.Containers  = new Dictionary<RDFResource, RDFModelEnums.RDFContainerType>();
            this.Collections = new Dictionary<RDFResource, RDFCollectionItem>();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Collects the namespaces used by the given triple
        /// </summary>
        private void CollectNamespaces(RDFTriple triple) {
            RDFNamespaceRegister.Instance.Register.ForEach(ns => {
                String nSpace            = ns.ToString();

                //Resolve subj Uri
                String subj              = triple.Subject.ToString();
                if (subj.Contains(nSpace) || subj.StartsWith(ns.Prefix + ":")) {
                    if (!this.Namespaces.Contains(ns)) {
                         this.Namespaces.Add(ns);
                    }
                }

                //Resolve pred Uri
                String pred              = triple.Predicate.ToString();
                if (pred.Contains(nSpace) || pred.StartsWith(ns.Prefix + ":")) {
                    if (!this.Namespaces.Contains(ns)) {
                         this.Namespaces.Add(ns);
                    }
                }

                //Resolve object Uri
                if (triple.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO) {
                    String obj           = triple.Object.ToString();
                    if (obj.Contains(nSpace) || obj.StartsWith(ns.Prefix + ":")) {
                        if (!this.Namespaces.Contains(ns)) {
                             this.Namespaces.Add(ns);
                        }
                    }
                }
                else {
                    //Resolve typed literal Uri
                    if (triple.Object is RDFTypedLiteral) {
                        String tLit      = RDFModelUtilities.GetDatatypeFromEnum(((RDFTypedLiteral)triple.Object).Datatype);
                        if (tLit.Contains(nSpace) || tLit.StartsWith(ns.Prefix + ":")) {
                            if (!this.Namespaces.Contains(ns)) {
                                 this.Namespaces.Add(ns);
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Verifies if the given triple carries a container subj and, if so, collects it
        /// </summary>
        private void CollectContainers(RDFTriple triple) {
            if (triple != null && triple.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO) {
                //SUBJECT -> rdf:type -> rdf:[Bag|Seq|Alt]
                if (triple.Predicate.Equals(RDFVocabulary.RDF.TYPE)) {
                    //rdf:Bag
                    if (triple.Object.Equals(RDFVocabulary.RDF.BAG)) {
                        if (!this.Containers.ContainsKey((RDFResource)triple.Subject)) {
                             this.Containers.Add((RDFResource)triple.Subject, RDFModelEnums.RDFContainerType.Bag);
                        }
                    }
                    //rdf:Seq
                    else if (triple.Object.Equals(RDFVocabulary.RDF.SEQ)) {
                        if (!this.Containers.ContainsKey((RDFResource)triple.Subject)) {
                             this.Containers.Add((RDFResource)triple.Subject, RDFModelEnums.RDFContainerType.Seq);
                        }
                    }
                    //rdf:Alt
                    else if (triple.Object.Equals(RDFVocabulary.RDF.ALT)) {
                        if (!this.Containers.ContainsKey((RDFResource)triple.Subject)) {
                             this.Containers.Add((RDFResource)triple.Subject, RDFModelEnums.RDFContainerType.Alt);
                        }
                    }
                }
            }
        }

         /// <summary>
        /// Verifies if the given triple carries a collection subj and, if so, collects it
        /// </summary>
        private void CollectCollections(RDFTriple triple) {
            if (triple != null) {
                //SUBJECT -> rdf:type -> rdf:list
                if (triple.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO && triple.Predicate.Equals(RDFVocabulary.RDF.TYPE)) {
                    if (triple.Object.Equals(RDFVocabulary.RDF.LIST)) {
                        if (!this.Collections.ContainsKey((RDFResource)triple.Subject)) {
                             this.Collections.Add((RDFResource)triple.Subject, new RDFCollectionItem(RDFModelEnums.RDFItemType.Resource, null, null));
                        }
                    }
                }
                //SUBJECT -> rdf:first -> [OBJECT|LITERAL]
                else if (triple.Predicate.Equals(RDFVocabulary.RDF.FIRST)) {
                    if (this.Collections.ContainsKey((RDFResource)triple.Subject)) {
                        if (triple.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO) {
                            this.Collections[(RDFResource)triple.Subject] = new RDFCollectionItem(RDFModelEnums.RDFItemType.Resource, (RDFResource)triple.Object, null);
                        }
                        else {
                            this.Collections[(RDFResource)triple.Subject] = new RDFCollectionItem(RDFModelEnums.RDFItemType.Literal,  (RDFLiteral)triple.Object,  null);
                        }
                    }
                }
                //SUBJECT -> rdf:rest -> [BNODE|RDF:NIL]
                else if (triple.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO && triple.Predicate.Equals(RDFVocabulary.RDF.REST)) {
                    if (this.Collections.ContainsKey((RDFResource)triple.Subject)) {
                        this.Collections[(RDFResource)triple.Subject] = new RDFCollectionItem(this.Collections[(RDFResource)triple.Subject].ItemType,
                                                                                              this.Collections[(RDFResource)triple.Subject].ItemValue, 
                                                                                              triple.Object);
                    }
                }
            }
        }

        /// <summary>
        /// Clears the metadata of the graph
        /// </summary>
        internal void ClearMetadata() {
            this.Namespaces.Clear();
            this.Containers.Clear();
            this.Collections.Clear();
        }

        /// <summary>
        /// Updates the metadata of the graph with the info carried by the given triple 
        /// </summary>
        internal void UpdateMetadata(RDFTriple triple) {
            if (triple != null){
                this.CollectNamespaces(triple);
                this.CollectContainers(triple);
                this.CollectCollections(triple);
            }
        }
        #endregion

    }

}