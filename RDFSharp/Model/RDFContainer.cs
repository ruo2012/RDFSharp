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

namespace RDFSharp.Model
{

    /// <summary>
    /// RDFContainer represents a generic container in the RDF model.
    /// It is made up of items, which must be all resources or all literals.
    /// </summary>
    public class RDFContainer: IEnumerable {

        #region Properties
        /// <summary>
        /// Type of the container
        /// </summary>
        public RDFModelEnums.RDFContainerType ContainerType { get; internal set; }
        
        /// <summary>
        /// Type of the items of the container
        /// </summary>
        public RDFModelEnums.RDFItemType ItemType { get; internal set; }

        /// <summary>
        /// Subject of the container's reification
        /// </summary>
        public RDFResource ReificationSubject { get; internal set; }

        /// <summary>
        /// Count of the container's items
        /// </summary>
        public Int32 ItemsCount {
            get { return this.Items.Count; }
        }

        /// <summary>
        /// Gets the enumerator on the container's items for iteration
        /// </summary>
        public IEnumerator ItemsEnumerator {
            get { return this.Items.GetEnumerator(); }
        }

        /// <summary>
        /// List of the items contained in the container
        /// </summary>
        internal ArrayList Items { get; set; }
        #endregion

        #region Ctors
        /// <summary>
        /// Default ctor to build an empty container of the given flavor and given type
        /// </summary>
        public RDFContainer(RDFModelEnums.RDFContainerType containerType, RDFModelEnums.RDFItemType itemType) {
            this.ContainerType          = containerType;
            this.ItemType               = itemType;
            this.ReificationSubject     = new RDFResource();
            this.Items                  = new ArrayList();
        }

        /// <summary>
        /// List-based ctor to build a container of the given flavor and given type with given items
        /// </summary>
        public RDFContainer(RDFModelEnums.RDFContainerType containerType, RDFModelEnums.RDFItemType itemType, ArrayList items): this(containerType, itemType) {
            if (items != null) {
                switch (this.ItemType) {
                    case RDFModelEnums.RDFItemType.Resource:
                        foreach (var item in items) {
                            if (item is RDFResource) {
                                this.AddItem(item);
                            }
                            else {
                                throw new RDFModelException("Cannot create RDFContainer because all the elements must be resources.");
                            }
                        }
                        break;
                    case RDFModelEnums.RDFItemType.Literal:
                        foreach (var item in items) {
                            if (item is RDFLiteral)  {
                                this.AddItem(item);
                            }
                            else {
                                throw new RDFModelException("Cannot create RDFContainer because all the elements must be literals.");
                            }
                        }
                        break;
                }
            }
        }
        #endregion

        #region Interfaces
        /// <summary>
        /// Exposes an untyped enumerator on the container's items
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() {
            return this.Items.GetEnumerator();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Adds the given item to the container
        /// </summary>
        public RDFContainer AddItem(Object item) {
            if (item != null) {

                //Try to add a resource
                if (item is RDFResource     && this.ItemType == RDFModelEnums.RDFItemType.Resource) {
                    //In case this is an "Alt" container, we do not allow duplicates
                    if (this.ContainerType   == RDFModelEnums.RDFContainerType.Alt) {
                        Boolean itemFound    = false;
                        foreach(var itemEnum in this) {
                            if (((RDFResource)itemEnum).Equals((RDFResource)item)) {
                                itemFound    = true;
                                break;
                            }
                        }
                        if (!itemFound) {
                             this.Items.Add(item);
                        }
                    }
                    //Else, we allow duplicates
                    else {
                        this.Items.Add(item);
                    }
                }

                //Try to add a literal
                else if (item is RDFLiteral && this.ItemType == RDFModelEnums.RDFItemType.Literal) {
                    //In case this is an "Alt" container, we do not allow duplicates
                    if (this.ContainerType  == RDFModelEnums.RDFContainerType.Alt) {
                        Boolean itemFound    = false;
                        foreach(var itemEnum in this) {
                            if (((RDFLiteral)itemEnum).Equals((RDFLiteral)item)) {
                                itemFound    = true;
                                break;
                            }
                        }
                        if (!itemFound) {
                             this.Items.Add(item);
                        }
                    }
                    //Else, we allow duplicates
                    else {
                        this.Items.Add(item);
                    }
                }

            }
            return this;
        }

        /// <summary>
        /// Removes the given item from the container
        /// </summary>
        public RDFContainer RemoveItem(Object item) {
            if (item != null) {

                //Try to remove a resource
                if (item is RDFResource && this.ItemType == RDFModelEnums.RDFItemType.Resource) {
                    ArrayList resultList = new ArrayList();
                    foreach(var itemEnum in this) {
                        if (!((RDFResource)itemEnum).Equals((RDFResource)item)) {
                            resultList.Add(itemEnum);
                        }
                    }
                    this.Items = resultList;
                }

                //Try to remove a literal
                else if (item is RDFLiteral && this.ItemType == RDFModelEnums.RDFItemType.Literal) {
                    ArrayList resultList = new ArrayList();
                    foreach(var itemEnum in this) {
                        if (!((RDFLiteral)itemEnum).Equals((RDFLiteral)item)) {
                            resultList.Add(itemEnum);
                        }
                    }
                    this.Items = resultList;
                }

            }
            return this;
        }

        /// <summary>
        /// Removes all the items from the container
        /// </summary>
        public RDFContainer ClearItems() {
            this.Items.Clear();
            return this;
        }

        /// <summary>
        /// Builds the reification graph of the container:
        /// Subject -> rdf:type -> [rdf:Bag|rdf:Seq|rdf:Alt] 
        /// Subject -> rdf:_N   -> RDFContainer.ITEM(N)
        /// </summary>
        public RDFGraph ReifyContainer() {
		    RDFGraph reifCont = new RDFGraph();

            //  Subject -> rdf:type -> [rdf:Bag|rdf:Seq|rdf:Alt] 
            switch (this.ContainerType) {
                case RDFModelEnums.RDFContainerType.Bag:
                     reifCont.AddTriple(new RDFTriple(this.ReificationSubject, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.BAG));
                     break;
                case RDFModelEnums.RDFContainerType.Seq:
                     reifCont.AddTriple(new RDFTriple(this.ReificationSubject, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.SEQ));
                     break;
                case RDFModelEnums.RDFContainerType.Alt:
                     reifCont.AddTriple(new RDFTriple(this.ReificationSubject, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.ALT));
                     break;
            }

            //  Subject -> rdf:_N -> RDFContainer.ITEM(N)
            Int32 index = 0;
            foreach (Object item in this) {
                RDFResource ordPred = new RDFResource(RDFVocabulary.RDF.BASE_URI + "_" + (++index));
                if (this.ItemType  == RDFModelEnums.RDFItemType.Resource) {
                    reifCont.AddTriple(new RDFTriple(this.ReificationSubject, ordPred, (RDFResource)item));
                }
                else {
                    reifCont.AddTriple(new RDFTriple(this.ReificationSubject, ordPred, (RDFLiteral)item));
                }
            }

            return reifCont;
        }
        #endregion

    }

}