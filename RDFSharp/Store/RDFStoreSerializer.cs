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

namespace RDFSharp.Store {

    /// <summary>
    /// RDFStoreSerializer exposes choices to read and write RDF store data in supported formats.
    /// </summary>
    public static class RDFStoreSerializer {

        #region Methods

        #region Write
        /// <summary>
        /// Writes the given store to the given file in the given RDF format. 
        /// </summary>
        public static void WriteRDF(RDFStoreEnums.RDFFormats rdfFormat, RDFStore store, String filepath) {
            if (store        != null) {
                if (filepath != null) {
                    switch (rdfFormat) {
                        case RDFStoreEnums.RDFFormats.NQuads:
                             RDFNQuads.Serialize(store, filepath);
                             break;
                        case RDFStoreEnums.RDFFormats.TriX:
                             RDFTriX.Serialize(store, filepath);
                             break;
                    }
                }
                else {
                    throw new RDFStoreException("Cannot write RDF file because given \"filepath\" parameter is null.");
                }
            }
            else {
                throw new RDFStoreException("Cannot write RDF file because given \"store\" parameter is null.");
            }
        }

        /// <summary>
        /// Writes the given store to the given stream in the given RDF format. 
        /// </summary>
        public static void WriteRDF(RDFStoreEnums.RDFFormats rdfFormat, RDFStore store, Stream outputStream) {
            if (store            != null) {
                if (outputStream != null) {
                    switch    (rdfFormat) {
                        case RDFStoreEnums.RDFFormats.NQuads:
                             RDFNQuads.Serialize(store, outputStream);
                             break;
                        case RDFStoreEnums.RDFFormats.TriX:
                             RDFTriX.Serialize(store, outputStream);
                             break;
                    }
                }
                else {
                    throw new RDFStoreException("Cannot write RDF file because given \"outputStream\" parameter is null.");
                }
            }
            else {
                throw new RDFStoreException("Cannot write RDF file because given \"store\" parameter is null.");
            }
        }
        #endregion

        #region Read
        /// <summary>
        /// Reads the given file in the given RDF format to a memory store. 
        /// </summary>
        public static RDFMemoryStore ReadRDF(RDFStoreEnums.RDFFormats rdfFormat, String filepath) {
            if (filepath != null) {
                if (File.Exists(filepath)) {
                    switch(rdfFormat) {
                        case RDFStoreEnums.RDFFormats.TriX:
                             return RDFTriX.Deserialize(filepath);
                        case RDFStoreEnums.RDFFormats.NQuads:
                             return RDFNQuads.Deserialize(filepath);
                    }
                }
                throw new RDFStoreException("Cannot read RDF file because given \"filepath\" parameter (" + filepath + ") does not indicate an existing file.");
            }
            throw new RDFStoreException("Cannot read RDF file because given \"filepath\" parameter is null.");
        }

        /// <summary>
        /// Reads the given stream in the given RDF format to a memory store. 
        /// </summary>
        public static RDFMemoryStore ReadRDF(RDFStoreEnums.RDFFormats rdfFormat, Stream inputStream) {
            if (inputStream != null) {
                switch (rdfFormat) {
                    case RDFStoreEnums.RDFFormats.TriX:
                         return RDFTriX.Deserialize(inputStream);
                    case RDFStoreEnums.RDFFormats.NQuads:
                         return RDFNQuads.Deserialize(inputStream);
                }
            }
            throw new RDFStoreException("Cannot read RDF stream because given \"inputStream\" parameter is null.");
        }
        #endregion

        #endregion

    }

}