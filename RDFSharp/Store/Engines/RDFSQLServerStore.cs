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
using System.Data.SqlClient;
using System.Linq;
using RDFSharp.Model;
using RDFSharp.Query;

namespace RDFSharp.Store
{

    /// <summary>
    /// RDFSQLServerStore represents a RDFStore backed on SQL Server engine
    /// </summary>
    public class RDFSQLServerStore: RDFStore {

        #region Properties
        /// <summary>
        /// Connection to the SQL Server database
        /// </summary>
        internal SqlConnection Connection { get; set; }
        #endregion

        #region Ctors
        /// <summary>
        /// Default-ctor to build a SQL Server store instance with SQL Server authentication
        /// </summary>
        public RDFSQLServerStore(String sqlServerInstance,
                                 String sqlServerDatabase,
                                 String sqlServerUserName,
                                 String sqlServerUserPwd) {
            if(sqlServerInstance            != null) {
                if(sqlServerDatabase        != null) {
                    if(sqlServerUserName    != null) {
                        if(sqlServerUserPwd != null) {

                            //Initialize store structures
                            this.StoreType   = "SQLSERVER";
                            this.Connection  = new SqlConnection(@"Server="    + sqlServerInstance +
                                                                  ";Database=" + sqlServerDatabase +
                                                                  ";User Id="  + sqlServerUserName +
                                                                  ";Password=" + sqlServerUserPwd  +
                                                                  ";Persist Security Info=false;");
                            this.StoreID     = RDFModelUtilities.CreateHash(this.ToString());

                            //Perform initial diagnostics
                            this.PrepareStore();

                        }
                        else {
                            throw new RDFStoreException("Cannot connect to SQL Server store because: given \"sqlServerUserPwd\" parameter is null.");
                        }
                    }
                    else {
                        throw new RDFStoreException("Cannot connect to SQL Server store because: given \"sqlServerUserName\" parameter is null.");
                    }
                }
                else {
                    throw new RDFStoreException("Cannot connect to SQL Server store because: given \"sqlServerDatabase\" parameter is null.");
                }
            }
            else {
                throw new RDFStoreException("Cannot connect to SQL Server store because: given \"sqlServerInstance\" parameter is null.");
            }
        }

        /// <summary>
        /// Default-ctor to build a SQL Server store instance with Windows Integrated Security authentication
        /// </summary>
        public RDFSQLServerStore(String sqlServerInstance,
                                 String sqlServerDatabase) {
            if(sqlServerInstance     != null) {
                if(sqlServerDatabase != null) {

                    //Initialize store structures
                    this.StoreType    = "SQLSERVER";
                    this.Connection   = new SqlConnection(@"Server="    + sqlServerInstance +
                                                           ";Database=" + sqlServerDatabase +
                                                           ";Integrated Security=true;Persist Security Info=false;");
                    this.StoreID      = RDFModelUtilities.CreateHash(this.ToString());

                    //Perform initial diagnostics
                    this.PrepareStore();

                }
                else {
                    throw new RDFStoreException("Cannot connect to SQL Server store because: given \"sqlServerDatabase\" parameter is null.");
                }
            }
            else {
                throw new RDFStoreException("Cannot connect to SQL Server store because: given \"sqlServerInstance\" parameter is null.");
            }
        }
        #endregion

        #region Interfaces
        /// <summary>
        /// Gives the string representation of the SQL Server store 
        /// </summary>
        public override String ToString() {
            return base.ToString() + "|SERVER=" + this.Connection.DataSource + ";DATABASE=" + this.Connection.Database;
        }
        #endregion

        #region Methods

        #region Add
        /// <summary>
        /// Merges the given graph into the store within a single transaction, avoiding duplicate insertions
        /// </summary>
        public override RDFStore MergeGraph(RDFGraph graph) {
            if (graph       != null) {
                var graphCtx = new RDFContext(graph.Context);

                //Create command
                var command  = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM [dbo].[Quadruples] WHERE [QuadrupleID] = @QID) BEGIN INSERT INTO [dbo].[Quadruples]([QuadrupleID], [TripleFlavor], [Context], [ContextID], [Subject], [SubjectID], [Predicate], [PredicateID], [Object], [ObjectID]) VALUES (@QID, @TFV, @CTX, @CTXID, @SUBJ, @SUBJID, @PRED, @PREDID, @OBJ, @OBJID) END", this.Connection);
                command.Parameters.Add(new SqlParameter("QID",    SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                command.Parameters.Add(new SqlParameter("CTX",    SqlDbType.VarChar, 1000));
                command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("SUBJ",   SqlDbType.VarChar, 1000));
                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("PRED",   SqlDbType.VarChar, 1000));
                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("OBJ",    SqlDbType.VarChar, 5000));
                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));

                try {

                    //Open connection
                    this.Connection.Open();
                    
                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Iterate triples
                    foreach(var triple in graph) {
                        
                        //Valorize parameters
                        command.Parameters["QID"].Value    = RDFModelUtilities.CreateHash(graphCtx         + " " +
                                                                                          triple.Subject   + " " +
                                                                                          triple.Predicate + " " +
                                                                                          triple.Object);
                        command.Parameters["TFV"].Value    = triple.TripleFlavor;
                        command.Parameters["CTX"].Value    = graphCtx.ToString();
                        command.Parameters["CTXID"].Value  = graphCtx.PatternMemberID;
                        command.Parameters["SUBJ"].Value   = triple.Subject.ToString();
                        command.Parameters["SUBJID"].Value = triple.Subject.PatternMemberID;
                        command.Parameters["PRED"].Value   = triple.Predicate.ToString();
                        command.Parameters["PREDID"].Value = triple.Predicate.PatternMemberID;
                        command.Parameters["OBJ"].Value    = triple.Object.ToString();
                        command.Parameters["OBJID"].Value  = triple.Object.PatternMemberID;
                        
                        //Execute command
                        command.ExecuteNonQuery();
                    }

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot insert data into SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }

        /// <summary>
        /// Adds the given quadruple to the store, avoiding duplicate insertions
        /// </summary>
        public override RDFStore AddQuadruple(RDFQuadruple quadruple) {
            if (quadruple   != null) {

                //Create command
                var command  = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM [dbo].[Quadruples] WHERE [QuadrupleID] = @QID) BEGIN INSERT INTO [dbo].[Quadruples]([QuadrupleID], [TripleFlavor], [Context], [ContextID], [Subject], [SubjectID], [Predicate], [PredicateID], [Object], [ObjectID]) VALUES (@QID, @TFV, @CTX, @CTXID, @SUBJ, @SUBJID, @PRED, @PREDID, @OBJ, @OBJID) END", this.Connection);
                command.Parameters.Add(new SqlParameter("QID",    SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                command.Parameters.Add(new SqlParameter("CTX",    SqlDbType.VarChar, 1000));
                command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("SUBJ",   SqlDbType.VarChar, 1000));
                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("PRED",   SqlDbType.VarChar, 1000));
                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("OBJ",    SqlDbType.VarChar, 5000));
                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));

                //Valorize parameters
                command.Parameters["QID"].Value    = quadruple.QuadrupleID;
                command.Parameters["TFV"].Value    = quadruple.TripleFlavor;
                command.Parameters["CTX"].Value    = quadruple.Context.ToString();
                command.Parameters["CTXID"].Value  = quadruple.Context.PatternMemberID;
                command.Parameters["SUBJ"].Value   = quadruple.Subject.ToString();
                command.Parameters["SUBJID"].Value = quadruple.Subject.PatternMemberID;
                command.Parameters["PRED"].Value   = quadruple.Predicate.ToString();
                command.Parameters["PREDID"].Value = quadruple.Predicate.PatternMemberID;
                command.Parameters["OBJ"].Value    = quadruple.Object.ToString();
                command.Parameters["OBJID"].Value  = quadruple.Object.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot insert data into SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }
        #endregion

        #region Remove
        /// <summary>
        /// Removes the given quadruples from the store
        /// </summary>
        public override RDFStore RemoveQuadruple(RDFQuadruple quadruple) {
            if (quadruple  != null) {

                //Create command
                var command = new SqlCommand("DELETE FROM [dbo].[Quadruples] WHERE [QuadrupleID] = @QID", this.Connection);
                command.Parameters.Add(new SqlParameter("QID", SqlDbType.BigInt));
                
                //Valorize parameters
                command.Parameters["QID"].Value = quadruple.QuadrupleID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();                    

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given context
        /// </summary>
        public override RDFStore RemoveQuadruplesByContext(RDFContext contextResource) {
            if (contextResource != null) {

                //Create command
                var command      = new SqlCommand("DELETE FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID", this.Connection);
                command.Parameters.Add(new SqlParameter("CTXID", SqlDbType.BigInt));

                //Valorize parameters
                command.Parameters["CTXID"].Value = contextResource.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given subject
        /// </summary>
        public override RDFStore RemoveQuadruplesBySubject(RDFResource subjectResource) {
            if (subjectResource != null) {

                //Create command
                var command      = new SqlCommand("DELETE FROM [dbo].[Quadruples] WHERE [SubjectID] = @SUBJID", this.Connection);
                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));

                //Valorize parameters
                command.Parameters["SUBJID"].Value = subjectResource.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given predicate
        /// </summary>
        public override RDFStore RemoveQuadruplesByPredicate(RDFResource predicateResource) {
            if (predicateResource != null) {

                //Create command
                var command        = new SqlCommand("DELETE FROM [dbo].[Quadruples] WHERE [PredicateID] = @PREDID", this.Connection);
                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));

                //Valorize parameters
                command.Parameters["PREDID"].Value = predicateResource.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given resource as object
        /// </summary>
        public override RDFStore RemoveQuadruplesByObject(RDFResource objectResource) {
            if (objectResource   != null) {

                //Create command
                var command       = new SqlCommand("DELETE FROM [dbo].[Quadruples] WHERE [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                command.Parameters.Add(new SqlParameter("OBJID", SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("TFV",   SqlDbType.Int));

                //Valorize parameters
                command.Parameters["OBJID"].Value = objectResource.PatternMemberID;
                command.Parameters["TFV"].Value   = RDFModelEnums.RDFTripleFlavor.SPO;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given literal as object
        /// </summary>
        public override RDFStore RemoveQuadruplesByLiteral(RDFLiteral literalObject) {
            if (literalObject    != null) {

                //Create command
                var command       = new SqlCommand("DELETE FROM [dbo].[Quadruples] WHERE [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                command.Parameters.Add(new SqlParameter("OBJID", SqlDbType.BigInt));
                command.Parameters.Add(new SqlParameter("TFV",   SqlDbType.Int));

                //Valorize parameters
                command.Parameters["OBJID"].Value = literalObject.PatternMemberID;
                command.Parameters["TFV"].Value   = RDFModelEnums.RDFTripleFlavor.SPL;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from SQL Server store because: " + ex.Message, ex);

                }
            }
            return this;
        }

        /// <summary>
        /// Clears the quadruples of the store
        /// </summary>
        public override RDFStore ClearQuadruples() {

            //Create command
            var command = new SqlCommand("DELETE FROM [dbo].[Quadruples]", this.Connection);
            
            try {

                //Open connection
                this.Connection.Open();

                //Prepare command
                command.Prepare();

                //Open transaction
                command.Transaction = this.Connection.BeginTransaction();

                //Execute command
                command.ExecuteNonQuery();

                //Close transaction
                command.Transaction.Commit();

                //Close connection
                this.Connection.Close();

            }
            catch (Exception ex) {

                //Rollback transaction
                command.Transaction.Rollback();

                //Close connection
                this.Connection.Close();

                //Propagate exception
                throw new RDFStoreException("Cannot delete data from SQL Server store because: " + ex.Message, ex);

            }
            return this;
        }

        /// <summary>
        /// Compacts the reified quadruples by removing their 4 standard statements 
        /// </summary>
        public override RDFStore UnreifyQuadruples() {

            //Create SPARQL SELECT query for detecting reified quadruples
            var T = new RDFVariable("T", true);
            var C = new RDFVariable("C", true);
            var S = new RDFVariable("S", true);
            var P = new RDFVariable("P", true);
            var O = new RDFVariable("O", true);
            var Q = new RDFSelectQuery()
                            .AddPatternGroup(new RDFPatternGroup("UnreifyQuadruples")
                                .AddPattern(new RDFPattern(C, T, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.STATEMENT))
                                .AddPattern(new RDFPattern(C, T, RDFVocabulary.RDF.SUBJECT, S))
                                .AddPattern(new RDFPattern(C, T, RDFVocabulary.RDF.PREDICATE, P))
                                .AddPattern(new RDFPattern(C, T, RDFVocabulary.RDF.OBJECT, O))
                                .AddFilter(new RDFIsUriFilter(C))
                                .AddFilter(new RDFIsUriFilter(T))
                                .AddFilter(new RDFIsUriFilter(S))
                                .AddFilter(new RDFIsUriFilter(P))
                            );

            //Apply it to the store
            var R = Q.ApplyToStore(this);

            //Iterate results
            var reifiedQuadruples = R.SelectResults.Rows.GetEnumerator();
            while (reifiedQuadruples.MoveNext()) {

                //Get reification data (T, C, S, P, O)
                var tRepresent = RDFQueryUtilities.ParseRDFPatternMember(((DataRow)reifiedQuadruples.Current)["?T"].ToString());
                var tContext   = RDFQueryUtilities.ParseRDFPatternMember(((DataRow)reifiedQuadruples.Current)["?C"].ToString());
                var tSubject   = RDFQueryUtilities.ParseRDFPatternMember(((DataRow)reifiedQuadruples.Current)["?S"].ToString());
                var tPredicate = RDFQueryUtilities.ParseRDFPatternMember(((DataRow)reifiedQuadruples.Current)["?P"].ToString());
                var tObject    = RDFQueryUtilities.ParseRDFPatternMember(((DataRow)reifiedQuadruples.Current)["?O"].ToString());

                //Cleanup store from detected reifications
                if (tObject is RDFResource) {
                    this.AddQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tSubject, (RDFResource)tPredicate, (RDFResource)tObject));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.STATEMENT));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.SUBJECT, (RDFResource)tSubject));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.PREDICATE, (RDFResource)tPredicate));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.OBJECT, (RDFResource)tObject));
                }
                else {
                    this.AddQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tSubject, (RDFResource)tPredicate, (RDFLiteral)tObject));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.TYPE, RDFVocabulary.RDF.STATEMENT));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.SUBJECT, (RDFResource)tSubject));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.PREDICATE, (RDFResource)tPredicate));
                    this.RemoveQuadruple(new RDFQuadruple(new RDFContext(((RDFResource)tContext).URI), (RDFResource)tRepresent, RDFVocabulary.RDF.OBJECT, (RDFLiteral)tObject));
                }

            }

            return this;
        }
        #endregion

        #region Select
        /// <summary>
        /// Gets a list containing the graphs saved in the store
        /// </summary>
        public override List<RDFGraph> ExtractGraphs() {
            var graphs      = new Dictionary<Int64, RDFGraph>();
            foreach (var q in this.SelectAllQuadruples()) {

                // Step 1: Cache-Update
                if (!graphs.ContainsKey(q.Context.PatternMemberID)) {
                     graphs.Add(q.Context.PatternMemberID, new RDFGraph());
                     graphs[q.Context.PatternMemberID].SetContext(((RDFContext)q.Context).Context);
                }

                // Step 2: Result-Update
                if (q.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO) {
                    graphs[q.Context.PatternMemberID].AddTriple(new RDFTriple((RDFResource)q.Subject,
                                                                              (RDFResource)q.Predicate,
                                                                              (RDFResource)q.Object));
                }
                else {
                    graphs[q.Context.PatternMemberID].AddTriple(new RDFTriple((RDFResource)q.Subject,
                                                                              (RDFResource)q.Predicate,
                                                                              (RDFLiteral)q.Object));
                }

            }
            return graphs.Values.ToList();
        }

        /// <summary>
        /// Checks if the store contains the given quadruple
        /// </summary>
        public override Boolean ContainsQuadruple(RDFQuadruple quadruple) {
            if (quadruple   != null) {
                if (quadruple.TripleFlavor == RDFModelEnums.RDFTripleFlavor.SPO) {
                    return (this.SelectQuadruples((RDFContext)quadruple.Context,
                                                  (RDFResource)quadruple.Subject,
                                                  (RDFResource)quadruple.Predicate,
                                                  (RDFResource)quadruple.Object,
                                                  null)).QuadruplesCount > 0;
                }
                else {
                    return (this.SelectQuadruples((RDFContext)quadruple.Context,
                                                  (RDFResource)quadruple.Subject,
                                                  (RDFResource)quadruple.Predicate,
                                                  null,
                                                  (RDFLiteral)quadruple.Object)).QuadruplesCount > 0;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets a store containing all quadruples
        /// </summary>
        public override RDFMemoryStore SelectAllQuadruples() {
            return this.SelectQuadruples(null, null, null, null, null);
        }

        /// <summary>
        /// Gets a memory store containing quadruples with the specified context
        /// </summary>
        public override RDFMemoryStore SelectQuadruplesByContext(RDFContext contextResource) {
            return this.SelectQuadruples(contextResource, null, null, null, null);
        }

        /// <summary>
        /// Gets a memory store containing quadruples with the specified subject
        /// </summary>
        public override RDFMemoryStore SelectQuadruplesBySubject(RDFResource subjectResource) {
            return this.SelectQuadruples(null, subjectResource, null, null, null);
        }

        /// <summary>
        /// Gets a memory store containing quadruples with the specified predicate
        /// </summary>
        public override RDFMemoryStore SelectQuadruplesByPredicate(RDFResource predicateResource) {
            return this.SelectQuadruples(null, null, predicateResource, null, null);
        }

        /// <summary>
        /// Gets a memory store containing quadruples with the specified object
        /// </summary>
        public override RDFMemoryStore SelectQuadruplesByObject(RDFResource objectResource) {
            return this.SelectQuadruples(null, null, null, objectResource, null);
        }

        /// <summary>
        /// Gets a memory store containing quadruples with the specified literal
        /// </summary>
        public override RDFMemoryStore SelectQuadruplesByLiteral(RDFLiteral objectLiteral) {
            return this.SelectQuadruples(null, null, null, null, objectLiteral);
        }

        /// <summary>
        /// Gets a memory store containing quadruples satisfying the given pattern
        /// </summary>
        internal override RDFMemoryStore SelectQuadruples(RDFContext  ctx,
                                                          RDFResource subj,
                                                          RDFResource pred,
                                                          RDFResource obj,
                                                          RDFLiteral  lit) {
            RDFMemoryStore result    = new RDFMemoryStore();
            SqlCommand command       = null;

            //Intersect the filters
            if (ctx                 != null) {
                if (subj            != null) {
                    if (pred        != null) {
                        if (obj     != null) {
                            //C->S->P->O
                            command  = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [SubjectID] = @SUBJID AND [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                            command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->S->P->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [SubjectID] = @SUBJID AND [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                                command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //C->S->P->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [SubjectID] = @SUBJID AND [PredicateID] = @PREDID", this.Connection);
                                command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //C->S->->O
                            command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [SubjectID] = @SUBJID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                            command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->S->->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [SubjectID] = @SUBJID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                                command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //C->S->->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [SubjectID] = @SUBJID", this.Connection);
                                command.Parameters.Add(new SqlParameter("CTXID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            }
                        }
                    }
                }
                else {
                    if (pred        != null) {
                        if (obj     != null) {
                            //C->->P->O
                            command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                            command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->->P->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                                command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //C->->P->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [PredicateID] = @PREDID", this.Connection);
                                command.Parameters.Add(new SqlParameter("CTXID",  SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //C->->->O
                            command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",   SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("CTXID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("OBJID", SqlDbType.BigInt));
                            command.Parameters["TFV"].Value   = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["CTXID"].Value = ctx.PatternMemberID;
                            command.Parameters["OBJID"].Value = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->->->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",   SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("CTXID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("OBJID", SqlDbType.BigInt));
                                command.Parameters["TFV"].Value   = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["CTXID"].Value = ctx.PatternMemberID;
                                command.Parameters["OBJID"].Value = lit.PatternMemberID;
                            }
                            else {
                                //C->->->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ContextID] = @CTXID", this.Connection);
                                command.Parameters.Add(new SqlParameter("CTXID", SqlDbType.BigInt));
                                command.Parameters["CTXID"].Value = ctx.PatternMemberID;
                            }
                        }
                    }
                }
            }
            else {
                if (subj            != null) {
                    if (pred        != null) {
                        if (obj     != null) {
                            //->S->P->O
                            command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [SubjectID] = @SUBJID AND [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                            command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->S->P->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [SubjectID] = @SUBJID AND [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                                command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //->S->P->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [SubjectID] = @SUBJID AND [PredicateID] = @PREDID", this.Connection);
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //->S->->O
                            command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [SubjectID] = @SUBJID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                            command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->S->->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [SubjectID] = @SUBJID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                                command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //->S->->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [SubjectID] = @SUBJID", this.Connection);
                                command.Parameters.Add(new SqlParameter("SUBJID", SqlDbType.BigInt));
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            }
                        }
                    }
                }
                else {
                    if (pred        != null) {
                        if (obj     != null) {
                            //->->P->O
                            command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                            command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                            command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->->P->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [PredicateID] = @PREDID AND [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",    SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters.Add(new SqlParameter("OBJID",  SqlDbType.BigInt));
                                command.Parameters["TFV"].Value    = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //->->P->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [PredicateID] = @PREDID", this.Connection);
                                command.Parameters.Add(new SqlParameter("PREDID", SqlDbType.BigInt));
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //->->->O
                            command  = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                            command.Parameters.Add(new SqlParameter("TFV",   SqlDbType.Int));
                            command.Parameters.Add(new SqlParameter("OBJID", SqlDbType.BigInt));
                            command.Parameters["TFV"].Value   = RDFModelEnums.RDFTripleFlavor.SPO;
                            command.Parameters["OBJID"].Value = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->->->L
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples] WHERE [ObjectID] = @OBJID AND [TripleFlavor] = @TFV", this.Connection);
                                command.Parameters.Add(new SqlParameter("TFV",   SqlDbType.Int));
                                command.Parameters.Add(new SqlParameter("OBJID", SqlDbType.BigInt));
                                command.Parameters["TFV"].Value   = RDFModelEnums.RDFTripleFlavor.SPL;
                                command.Parameters["OBJID"].Value = lit.PatternMemberID;
                            }
                            else {
                                //->->->
                                command = new SqlCommand("SELECT [TripleFlavor], [Context], [Subject], [Predicate], [Object] FROM [dbo].[Quadruples]", this.Connection);
                            }
                        }
                    }
                }
            }

            //Prepare and execute command
            try {

                //Open connection
                this.Connection.Open();
                
                //Prepare command
                command.Prepare();
                
                //Execute command
                using (var quadruples = command.ExecuteReader()) {
                    if(quadruples.HasRows) {
                        while(quadruples.Read()) {
                            result.AddQuadruple(RDFStoreUtilities.ParseQuadruple(quadruples));
                        }
                    }
                }

                //Close connection
                this.Connection.Close();
                
            }
            catch (Exception ex) {

                //Close connection
                this.Connection.Close();
                
                //Propagate exception
                throw new RDFStoreException("Cannot read data from SQL Server store because: " + ex.Message, ex);

            }

            return result;
        }
        #endregion

		#region Diagnostics
        /// <summary>
        /// Performs the preliminary diagnostics controls on the underlying SQL Server database
        /// </summary>
        private RDFStoreEnums.RDFStoreSQLErrors Diagnostics() {
            try {
                
                //Open connection
                this.Connection.Open();
                
                //Create command
                var command     = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name='Quadruples' AND type_desc='USER_TABLE'", this.Connection);
                
                //Execute command
                var result      = Int32.Parse(command.ExecuteScalar().ToString());
                
                //Close connection
                this.Connection.Close();                

                //Return the diagnostics state
                return (result == 0 ? RDFStoreEnums.RDFStoreSQLErrors.QuadruplesTableNotFound : RDFStoreEnums.RDFStoreSQLErrors.NoErrors);

            }
            catch {

                //Close connection
                this.Connection.Close();

                //Return the diagnostics state
                return RDFStoreEnums.RDFStoreSQLErrors.InvalidDataSource;

            }
        }

        /// <summary>
        /// Prepares the underlying SQL Server database
        /// </summary>
        private void PrepareStore() {
            var check           = this.Diagnostics();

            //Prepare the database only if diagnostics has detected the missing of "Quadruples" table in the store
            if (check          == RDFStoreEnums.RDFStoreSQLErrors.QuadruplesTableNotFound) {
                try {

                    //Open connection
                    this.Connection.Open();

                    //Create command
                    var command = new SqlCommand("CREATE TABLE [dbo].[Quadruples] ([QuadrupleID] BIGINT PRIMARY KEY NOT NULL, [TripleFlavor] INTEGER NOT NULL, [Context] VARCHAR(1000) NOT NULL, [ContextID] BIGINT NOT NULL, [Subject] VARCHAR(1000) NOT NULL, [SubjectID] BIGINT NOT NULL, [Predicate] VARCHAR(1000) NOT NULL, [PredicateID] BIGINT NOT NULL, [Object] VARCHAR(5000) NOT NULL, [ObjectID] BIGINT NOT NULL); CREATE NONCLUSTERED INDEX [IDX_ContextID] ON [dbo].[Quadruples]([ContextID]);CREATE NONCLUSTERED INDEX [IDX_SubjectID] ON [dbo].[Quadruples]([SubjectID]);CREATE NONCLUSTERED INDEX [IDX_PredicateID] ON [dbo].[Quadruples]([PredicateID]);CREATE NONCLUSTERED INDEX [IDX_ObjectID] ON [dbo].[Quadruples]([ObjectID],[TripleFlavor]);CREATE NONCLUSTERED INDEX [IDX_SubjectID_PredicateID] ON [dbo].[Quadruples]([SubjectID],[PredicateID]);CREATE NONCLUSTERED INDEX [IDX_SubjectID_ObjectID] ON [dbo].[Quadruples]([SubjectID],[ObjectID],[TripleFlavor]);CREATE NONCLUSTERED INDEX [IDX_PredicateID_ObjectID] ON [dbo].[Quadruples]([PredicateID],[ObjectID],[TripleFlavor]);", this.Connection);

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {
                    
                    //Close connection
                    this.Connection.Close();
                    
                    //Propagate exception
                    throw new RDFStoreException("Cannot prepare SQL Server store because: " + ex.Message, ex);

                }
            }

            //Otherwise, an exception must be thrown because it has not been possible to connect to the instance/database
            else if (check     == RDFStoreEnums.RDFStoreSQLErrors.InvalidDataSource) {
                throw new RDFStoreException("Cannot prepare SQL Server store because: unable to connect to the server instance or to open the selected database.");
            }
        }
        #endregion		
		
        #region Optimize
        /// <summary>
        /// Executes a special command to optimize SQL Server store
        /// </summary>
        public void OptimizeStore() {
            try {

                //Open connection
                this.Connection.Open();

                //Create command
                var command = new SqlCommand("ALTER INDEX ALL ON [dbo].[Quadruples] REORGANIZE;", this.Connection);

                //Execute command
                command.ExecuteNonQuery();

                //Close connection
                this.Connection.Close();

            }
            catch (Exception ex) {

                //Close connection
                this.Connection.Close();

                //Propagate exception
                throw new RDFStoreException("Cannot optimize SQL Server store because: " + ex.Message, ex);

            }
        }
        #endregion

        #endregion

    }

}