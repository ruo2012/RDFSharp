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
using System.Data;
using System.Linq;
using System.Text;
using RDFSharp.Model;
using RDFSharp.Store;

namespace RDFSharp.Query {

    /// <summary>
    /// RDFDescribeQuery is the SPARQL "DESCRIBE" query implementation.
    /// </summary>
    public class RDFDescribeQuery {

        #region Properties
        /// <summary>
        /// List of RDF terms to be described by the query
        /// </summary>
        internal List<RDFPatternMember> DescribeTerms { get; set; }

        /// <summary>
        /// List of body pattern groups carried by the query
        /// </summary>
        internal List<RDFPatternGroup> PatternGroups { get; set; }

        /// <summary>
        /// Dictionary of pattern result tables
        /// </summary>
        internal Dictionary<RDFPatternGroup, List<DataTable>> PatternResultTables { get; set; }

        /// <summary>
        /// Dictionary of pattern group result tables
        /// </summary>
        internal Dictionary<RDFPatternGroup, DataTable> PatternGroupResultTables { get; set; }

        /// <summary>
        /// List of variables carried by the template patterns of the query
        /// </summary>
        internal List<RDFVariable> Variables { get; set; }

        /// <summary>
        /// List of modifiers carried by the query
        /// </summary>
        internal List<RDFModifier> Modifiers { get; set; }

        /// <summary>
        /// Checks if the query is a "DESCRIBE *" query
        /// </summary>
        public Boolean IsStar {
            get {
                return (this.DescribeTerms.Count == 0);
            }
        }

        /// <summary>
        /// Checks if the query is empty, so contains no pattern groups
        /// </summary>
        public Boolean IsEmpty {
            get {
                return (this.PatternGroups.Count == 0);
            }
        }
        #endregion

        #region Ctors
        /// <summary>
        /// Default-ctor to build an empty DESCRIBE query
        /// </summary>
        public RDFDescribeQuery() {
            this.DescribeTerms            = new List<RDFPatternMember>();
            this.PatternGroups            = new List<RDFPatternGroup>();
            this.PatternResultTables      = new Dictionary<RDFPatternGroup, List<DataTable>>();
            this.PatternGroupResultTables = new Dictionary<RDFPatternGroup, DataTable>();
            this.Variables                = new List<RDFVariable>();
            this.Modifiers                = new List<RDFModifier>();
        }
        #endregion

        #region Interfaces
        /// <summary>
        /// Gives the string representation of the DESCRIBE query
        /// </summary>
        public override String ToString() {
            StringBuilder query    = new StringBuilder();

            if (this.DescribeTerms.Any() || this.PatternGroups.Any()) {

                // Describe
                query.Append("DESCRIBE");

                // TERMS
                if (!this.IsStar) {
                    this.DescribeTerms.ForEach(t => query.Append(" " + RDFQueryUtilities.PrintRDFPatternMember(t)));
                }
                else {
                    query.Append(" *");
                }

                // PATTERN GROUPS
                if(!this.IsEmpty) {
                    query.Append("\nWHERE{\n");                
                    Boolean printingUnion         = false;
                    this.PatternGroups.ForEach(pg => {

                        //Current pattern group is set as UNION with the next one
                        if (pg.JoinAsUnion) {

                            //Current pattern group IS NOT the last of the query (so UNION keyword must be appended at last)
                            if (!pg.Equals(this.PatternGroups.Last())) {
                                //Begin a new Union block
                                if (!printingUnion) {
                                    printingUnion = true;
                                    query.Append("\n  {");
                                }
                                query.Append(pg.ToString(2) + "    UNION");
                            }

                            //Current pattern group IS the last of the query (so UNION keyword must not be appended at last)
                            else {
                                //End the Union block
                                if (printingUnion) {
                                    printingUnion = false;
                                    query.Append(pg.ToString(2));
                                    query.Append("  }\n");
                                }
                                else {
                                    query.Append(pg.ToString());
                                }
                            }

                        }

                        //Current pattern group is set as INTERSECT with the next one
                        else {
                            //End the Union block
                            if (printingUnion) {
                                printingUnion     = false;
                                query.Append(pg.ToString(2));
                                query.Append("  }\n");
                            }
                            else {
                                query.Append(pg.ToString());
                            }
                        }

                    });
                    query.Append("\n}");
                }

                // MODIFIERS
                // LIMIT/OFFSET
                if (this.Modifiers.Any(mod => mod is RDFLimitModifier || mod is RDFOffsetModifier)) {
                    this.Modifiers.FindAll(mod => mod is RDFLimitModifier).ForEach(lim  => query.Append("\n" + lim));
                    this.Modifiers.FindAll(mod => mod is RDFOffsetModifier).ForEach(off => query.Append("\n" + off));
                }

            }

            return query.ToString();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Adds the given resource to the describe terms of the query
        /// </summary>
        public RDFDescribeQuery AddDescribeTerm(RDFResource describeRes) {
            if (describeRes != null) {
                if (!this.DescribeTerms.Exists(dt => dt.Equals(describeRes))) {
                    this.DescribeTerms.Add(describeRes);
                }
            }
            return this;
        }

        /// <summary>
        /// Adds the given variable to the describe terms of the query
        /// </summary>
        public RDFDescribeQuery AddDescribeTerm(RDFVariable describeVar) {
            if (describeVar != null) {
                if (!this.DescribeTerms.Exists(dt => dt.Equals(describeVar))) {
                    this.DescribeTerms.Add(describeVar);

                    //Variable
                    if (!this.Variables.Exists(v => v.Equals(describeVar))) {
                        this.Variables.Add(describeVar);
                    }

                }
            }
            return this;
        }

        /// <summary>
        /// Adds the given pattern group to the body of the query
        /// </summary>
        public RDFDescribeQuery AddPatternGroup(RDFPatternGroup patternGroup) {
            if (patternGroup != null) {
                if (!this.PatternGroups.Exists(pg => pg.PatternGroupName.Equals(patternGroup.PatternGroupName, StringComparison.Ordinal))) {
                    this.PatternGroups.Add(patternGroup);
                }
            }
            return this;
        }

        /// <summary>
        /// Adds the given modifier to the query
        /// </summary>
        public RDFDescribeQuery AddModifier(RDFLimitModifier modifier) {
            if(modifier != null) {
                if (!this.Modifiers.Any(m => m is RDFLimitModifier)) {
                    this.Modifiers.Add(modifier);
                }
            }
            return this;
        }

        /// <summary>
        /// Adds the given modifier to the query
        /// </summary>
        public RDFDescribeQuery AddModifier(RDFOffsetModifier modifier) {
            if(modifier != null) {
                if (!this.Modifiers.Any(m => m is RDFOffsetModifier)) {
                    this.Modifiers.Add(modifier);
                }
            }
            return this;
        }

        /// <summary>
        /// Applies the query to the given graph 
        /// </summary>
        public RDFDescribeQueryResult ApplyToGraph(RDFGraph graph) {
            if (graph != null) {
                this.PatternGroupResultTables.Clear();
                this.PatternResultTables.Clear();

                RDFDescribeQueryResult describeResult  = new RDFDescribeQueryResult(this.ToString());
                if (!this.IsEmpty) {

                    //Iterate the pattern groups of the query
                    foreach (RDFPatternGroup patternGroup in this.PatternGroups) {

                        //Step 1: Get the intermediate result tables of the current pattern group
                        RDFDescribeQueryEngine.EvaluatePatterns(this, patternGroup, graph);

                        //Step 2: Get the result table of the current pattern group
                        RDFDescribeQueryEngine.CombinePatterns(this, patternGroup);

                        //Step 3: Apply the filters of the current pattern group to its result table
                        RDFDescribeQueryEngine.ApplyFilters(this, patternGroup);

                    }

                    //Step 4: Get the result table of the query
                    DataTable queryResultTable         = RDFQueryEngine.CombineTables(this.PatternGroupResultTables.Values.ToList<DataTable>(), false);

                    //Step 5: Describe the terms from the result table
                    DataTable describeResultTable      = RDFDescribeQueryEngine.DescribeTerms(this, graph, queryResultTable);

                    //Step 6: Apply the modifiers of the query to the result table
                    describeResult.DescribeResults     = RDFDescribeQueryEngine.ApplyModifiers(this, describeResultTable);

                }
                else {

                    //In this case the only chance to proceed is to have resources in the describe terms,
                    //which will be used to search for S-P-O data. Variables are omitted in this scenario.
                    if (this.DescribeTerms.Any(dt => dt is RDFResource)) {

                        //Step 1: Describe the terms from the result table
                        DataTable describeResultTable  = RDFDescribeQueryEngine.DescribeTerms(this, graph, new DataTable());

                        //Step 2: Apply the modifiers of the query to the result table
                        describeResult.DescribeResults = RDFDescribeQueryEngine.ApplyModifiers(this, describeResultTable);

                    }

                }

                return describeResult;
            }
            throw new RDFQueryException("Cannot execute DESCRIBE query because given \"graph\" parameter is null.");
        }

        /// <summary>
        /// Applies the query to the given store 
        /// </summary>
        public RDFDescribeQueryResult ApplyToStore(RDFStore store) {
            if (store != null) {
                this.PatternGroupResultTables.Clear();
                this.PatternResultTables.Clear();

                RDFDescribeQueryResult describeResult  = new RDFDescribeQueryResult(this.ToString());
                if (!this.IsEmpty) {

                    //Iterate the pattern groups of the query
                    foreach (RDFPatternGroup patternGroup in this.PatternGroups) {

                        //Step 1: Get the intermediate result tables of the current pattern group
                        RDFDescribeQueryEngine.EvaluatePatterns(this, patternGroup, store);

                        //Step 2: Get the result table of the current pattern group
                        RDFDescribeQueryEngine.CombinePatterns(this, patternGroup);

                        //Step 3: Apply the filters of the current pattern group to its result table
                        RDFDescribeQueryEngine.ApplyFilters(this, patternGroup);

                    }

                    //Step 4: Get the result table of the query
                    DataTable queryResultTable         = RDFQueryEngine.CombineTables(this.PatternGroupResultTables.Values.ToList<DataTable>(), false);

                    //Step 5: Describe the terms from the result table
                    DataTable describeResultTable      = RDFDescribeQueryEngine.DescribeTerms(this, store, queryResultTable);

                    //Step 6: Apply the modifiers of the query to the result table
                    describeResult.DescribeResults     = RDFDescribeQueryEngine.ApplyModifiers(this, describeResultTable);

                }
                else {

                    //In this case the only chance to proceed is to have resources in the describe terms,
                    //which will be used to search for S-P-O data. Variables are omitted in this scenario.
                    if (this.DescribeTerms.Any(dt => dt is RDFResource)) {

                        //Step 1: Describe the terms from the result table
                        DataTable describeResultTable  = RDFDescribeQueryEngine.DescribeTerms(this, store, new DataTable());

                        //Step 2: Apply the modifiers of the query to the result table
                        describeResult.DescribeResults = RDFDescribeQueryEngine.ApplyModifiers(this, describeResultTable);

                    }

                }

                return describeResult;
            }
            throw new RDFQueryException("Cannot execute DESCRIBE query because given \"store\" parameter is null.");
        }

        /// <summary>
        /// Applies the query to the given federation
        /// </summary>
        public RDFDescribeQueryResult ApplyToFederation(RDFFederation federation) {
            if (federation != null) {
                this.PatternGroupResultTables.Clear();
                this.PatternResultTables.Clear();

                RDFDescribeQueryResult describeResult  = new RDFDescribeQueryResult(this.ToString());
                if (!this.IsEmpty) {

                    //Iterate the pattern groups of the query
                    var fedPatternResultTables         = new Dictionary<RDFPatternGroup, List<DataTable>>();
                    foreach (RDFPatternGroup patternGroup in this.PatternGroups) {

                        #region TrueFederations
                        foreach (RDFStore store in federation.Stores.Values) {

                            //Step 1: Evaluate the patterns of the current pattern group on the current store
                            RDFDescribeQueryEngine.EvaluatePatterns(this, patternGroup, store);

                            //Step 2: Federate the patterns of the current pattern group on the current store
                            if (!fedPatternResultTables.ContainsKey(patternGroup)) {
                                fedPatternResultTables.Add(patternGroup, this.PatternResultTables[patternGroup]);
                            }
                            else {
                                fedPatternResultTables[patternGroup].ForEach(fprt => 
                                    fprt.Merge(this.PatternResultTables[patternGroup].Single(prt => prt.TableName.Equals(fprt.TableName, StringComparison.Ordinal)), true, MissingSchemaAction.Add));
                            }

                        }
                        this.PatternResultTables[patternGroup] = fedPatternResultTables[patternGroup];
                        #endregion

                        //Step 3: Get the result table of the current pattern group
                        RDFDescribeQueryEngine.CombinePatterns(this, patternGroup);

                        //Step 4: Apply the filters of the current pattern group to its result table
                        RDFDescribeQueryEngine.ApplyFilters(this, patternGroup);

                    }

                    //Step 5: Get the result table of the query
                    DataTable queryResultTable         = RDFQueryEngine.CombineTables(this.PatternGroupResultTables.Values.ToList<DataTable>(), false);

                    //Step 6: Describe the terms on each store and merge them in the federated result table
                    DataTable describeResultTable      = new DataTable(this.ToString());
                    foreach (RDFStore store in federation.Stores.Values) {
                        describeResultTable.Merge(RDFDescribeQueryEngine.DescribeTerms(this, store, queryResultTable), true, MissingSchemaAction.Add);
                    }

                    //Step 7: Apply the modifiers of the query to the result table
                    describeResult.DescribeResults     = RDFDescribeQueryEngine.ApplyModifiers(this, describeResultTable);

                }
                else {

                    //In this case the only chance to proceed is to have resources in the describe terms,
                    //which will be used to search for S-P-O data. Variables are ignored in this scenario.
                    if (this.DescribeTerms.Any(dt => dt is RDFResource)) {

                        //Step 1: Describe the terms on each store and merge them in the federated result table
                        DataTable describeResultTable  = new DataTable(this.ToString());
                        foreach (RDFStore store in federation.Stores.Values) {
                            describeResultTable.Merge(RDFDescribeQueryEngine.DescribeTerms(this, store, new DataTable()), true, MissingSchemaAction.Add);
                        }

                        //Step 2: Apply the modifiers of the query to the result table
                        describeResult.DescribeResults = RDFDescribeQueryEngine.ApplyModifiers(this, describeResultTable);

                    }

                }

                return describeResult;
            }
            throw new RDFQueryException("Cannot execute DESCRIBE query because given \"federation\" parameter is null.");
        }
        #endregion

    }

}