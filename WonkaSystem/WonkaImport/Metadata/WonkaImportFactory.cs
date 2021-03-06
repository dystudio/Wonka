﻿using System;
using System.Collections.Generic;
using System.Data;
// using System.Data.Entity.Core.Objects;
// using System.Data.Entity.Core.Metadata.Edm;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata; 
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Wonka.BizRulesEngine;
using Wonka.MetaData;

namespace Wonka.Import.Metadata
{
    /// <summary>
    /// 
    /// This extensions class provides additional functionality for the Rules Engine, including the ability to
    /// import a schema from a database table and designate that as an IMetadataRetrievable instance.
    /// 
    /// NOTE: UNDER CONSTRUCTION
    /// 
    /// </summary>
    public class WonkaImportFactory
    {
        #region CONSTANTS

        public const int CONST_DEFAULT_GROUP_ID = 1;
        public const int CONST_SEC_LEVEL_READ   = 1;

        public const string CONST_VALIDATE_TABLE_SQL =
@"
select * 
  from INFORMATION_SCHEMA.TABLES
 where TABLE_NAME = @tname
";

        public const string CONST_SAMPLE_RULE_FORMAT_MAIN_BODY =
@"<?xml version=""1.0""?>
<RuleTree xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">

   <if description=""Sample Rules Body"">
      <criteria op=""AND"">
         <eval id=""pop1"">(N.{0}) POPULATED</eval>
      </criteria>

      <if description=""Checking Input Values"">
         <criteria op=""AND"">
            <eval id=""pop2"">(N.{1}) POPULATED</eval>
         </criteria>

         {2}

      </if>

      {3}

   </if>    
    
</RuleTree>";

        public const string CONST_SAMPLE_RULE_FORMAT_SUB_BODY1 =
@"
         <validate err=""severe"">
            <criteria op=""AND"">
               <eval id=""cmp2"">(N.{0}) GT (0.00)</eval>
               <eval id=""cmp3"">(N.{1}) GT (0.00)</eval>
            </criteria>

            <failure_message>ERROR!  Required inputs have not been provided.</failure_message>
            <success_message/>
         </validate>
";

        public const string CONST_SAMPLE_RULE_FORMAT_SUB_BODY2 =
@"      
      <if description=""Executing "">
         <criteria op=""AND"">
            <eval id=""cmp4"">(N.{0}) == ('DummyVal1')</eval>
            <eval id=""cmp5"">(N.{1}) IN ('DummyVal2','DummyVal3', 'DummyVal4')</eval>
         </criteria>

         <validate err=""severe"">
            <criteria op=""AND"">
               <eval id=""asn1"">(N.{1}) ASSIGN ('DummyValX')</eval>
            </criteria>

            <failure_message>ERROR!  Unable to assign the value.</failure_message>
            <success_message/>
         </validate>
      </if>
";            

        #endregion

        private static object mLock   = new object();
        private static object mIdLock = new object();

        private static int mGenAttrId = 1;

        private static WonkaImportFactory mInstance = null;

        Dictionary<string, IMetadataRetrievable> moCachedImports;

        private WonkaImportFactory()
        {
            moCachedImports = new Dictionary<string, IMetadataRetrievable>();
        }

        static private WonkaImportFactory CreateInstance()
        {
            lock (mLock)
            {
                if (mInstance == null)
                    mInstance = new WonkaImportFactory();

                return mInstance;
            }
        }

        static public WonkaImportFactory GetInstance()
        {
            lock (mLock)
            {
                if (mInstance == null)
                    mInstance = CreateInstance();

                return mInstance;
            }
        }

        #region Instance Methods

        private void CacheImport(string psDatabaseTable, IMetadataRetrievable poSource)
        {
            if (!String.IsNullOrEmpty(psDatabaseTable) && (poSource != null))
                moCachedImports[psDatabaseTable] = poSource;
            else
                throw new WonkaBizRuleException(0, 0, "ERROR!  Could not cache the schema for the database table.");
        }

        private int GenerateNewAttrId()
        {
            lock (mIdLock)
            {
                return mGenAttrId++;
            }
        }

        static public string CreateRulesSampleFile(IMetadataRetrievable piMetadata, string psRulesOutputFile)
        {
            StringBuilder sbRulesBody = new StringBuilder();

            if (piMetadata != null)
            {
                var AttrCache = piMetadata.GetAttrCache();

                if (AttrCache.Count >= 2)
                {
                    string sChildBranch1 = "";
                    string sChildBranch2 = "";

                    var AttrNumCache = AttrCache.Where(x => x.IsDecimal || x.IsNumeric);
                    var AttrStrCache = AttrCache.Where(x => !x.IsDecimal && !x.IsNumeric);

                    if (AttrNumCache.Count() >= 2)
                    {
                        sChildBranch1 =
                            String.Format(CONST_SAMPLE_RULE_FORMAT_SUB_BODY1,
                                          AttrNumCache.ElementAt(0).AttrName,
                                          AttrNumCache.ElementAt(1).AttrName);
                    }
                    else if (AttrNumCache.Count() == 1)
                    {
                        sChildBranch1 =
                            String.Format(CONST_SAMPLE_RULE_FORMAT_SUB_BODY1,
                                          AttrNumCache.ElementAt(0).AttrName,
                                          AttrNumCache.ElementAt(0).AttrName);
                    }

                    if (AttrStrCache.Count() >= 4)
                    {
                        sChildBranch2 =
                            String.Format(CONST_SAMPLE_RULE_FORMAT_SUB_BODY2,
                                          AttrStrCache.ElementAt(2).AttrName,
                                          AttrStrCache.ElementAt(3).AttrName);
                    }
                    else if (AttrStrCache.Count() == 3)
                    {
                        sChildBranch2 =
                            String.Format(CONST_SAMPLE_RULE_FORMAT_SUB_BODY2,
                                          AttrStrCache.ElementAt(1).AttrName,
                                          AttrStrCache.ElementAt(2).AttrName);
                    }
                    else
                    {
                        sChildBranch2 =
                            String.Format(CONST_SAMPLE_RULE_FORMAT_SUB_BODY2,
                                          AttrStrCache.ElementAt(0).AttrName,
                                          AttrStrCache.ElementAt(1).AttrName);
                    }

                    string sParentBranch = 
                        String.Format(CONST_SAMPLE_RULE_FORMAT_MAIN_BODY, 
                                      AttrCache[0].AttrName, 
                                      AttrCache[1].AttrName,
                                      sChildBranch1,
                                      sChildBranch2);

                    sbRulesBody.Append(sParentBranch);

                }
            }

            if (!String.IsNullOrEmpty(psRulesOutputFile))
            {
                FileInfo OutputFile = new FileInfo(psRulesOutputFile);

                if (OutputFile.Directory.Exists)
                    File.WriteAllText(psRulesOutputFile, sbRulesBody.ToString());
            }

            return sbRulesBody.ToString();
        }

        public IMetadataRetrievable ImportSource(string psDatabaseTable, DbContext poDbContext)
        {
            return ImportSource(psDatabaseTable, poDbContext.Model);
        }

        public IMetadataRetrievable ImportSource(string psDatabaseTable, IModel poDbContext)
        {
            WonkaImportSource NewImportSource = new WonkaImportSource();
            HashSet<string>   KeyColNames     = new HashSet<string>();

            IEntityType FoundTable = null;

            if (!String.IsNullOrEmpty(psDatabaseTable) && (poDbContext != null))
            {
                if (moCachedImports.ContainsKey(psDatabaseTable))
                    return moCachedImports[psDatabaseTable];

                var tables = poDbContext.GetEntityTypes(psDatabaseTable);

                foreach (var TmpTable in tables)
                {
                    if (TmpTable.Name == psDatabaseTable)
                    {
                        FoundTable = TmpTable;

                        var KeyCols = TmpTable.GetDeclaredKeys();
                        foreach (var KeyCol in KeyCols)
                            KeyColNames.Add(KeyCol.GetName());

                        break;
                    }
                }

                if (FoundTable == null)
                    throw new WonkaImportException("ERROR!  Table (" + psDatabaseTable + ") was not found in the provided DbContext.");

                var columns = 
                  from p in FoundTable.GetProperties()
                  select new
                  {
                      colName   = p.Name,
                      colType   = p.GetColumnType(),
                      maxLength = p.GetMaxLength(),
                      precision = 0,
                      scale     = 0,
                      defValue  = p.GetDefaultValue()
                  };

                /*
                var columns =
                    from meta in poDbContext.MetadataWorkspace.GetItems(DataSpace.CSpace).Where(m => m.BuiltInTypeKind == BuiltInTypeKind.EntityType)
                    from p in (meta as EntityType).Properties.Where(p => p.DeclaringType.Name == psDatabaseTable)
                    select new
                    {
                        colName   = p.Name,                       
                        colType   = p.TypeUsage.EdmType,
                        doc       = p.Documentation,
                        maxLength = p.MaxLength,
                        precision = p.Precision,
                        scale     = p.Scale,
                        defValue  = p.DefaultValue,
                        props     = p.MetadataProperties
                    };
                */

                foreach (var TmpCol in columns)
                {
                    string sTmpColName = TmpCol.colName;

                    WonkaRefAttr TmpWonkaAttr = new WonkaRefAttr();

                    TmpWonkaAttr.AttrId   = GenerateNewAttrId();
                    TmpWonkaAttr.AttrName = sTmpColName;
                    TmpWonkaAttr.ColName  = sTmpColName;
                    TmpWonkaAttr.TabName  = psDatabaseTable;

                    TmpWonkaAttr.DefaultValue = Convert.ToString(TmpCol.defValue);
                    // TmpWonkaAttr.Description  = (TmpCol.doc != null) ? TmpCol.doc.LongDescription : "";

                    TmpWonkaAttr.IsDate    = IsTypeDate(TmpCol.colType);
                    TmpWonkaAttr.IsNumeric = IsTypeNumeric(TmpCol.colType);
                    TmpWonkaAttr.IsDecimal = IsTypeDecimal(TmpCol.colType);

                    if (TmpWonkaAttr.IsNumeric || TmpWonkaAttr.IsDecimal)
                    {
                        // TmpWonkaAttr.Precision = (int) ((TmpCol.precision != null) ? TmpCol.precision : 0);
                        // TmpWonkaAttr.Scale     = (int) ((TmpCol.scale != null) ? TmpCol.scale : 0);
                        TmpWonkaAttr.Precision = TmpCol.precision;
                        TmpWonkaAttr.Scale     = TmpCol.scale;
                    }

                    TmpWonkaAttr.MaxLength = (TmpCol.maxLength != null) ? (int)TmpCol.maxLength : 0;

                    TmpWonkaAttr.FieldId   = TmpWonkaAttr.AttrId + 1000;
                    TmpWonkaAttr.GroupId   = CONST_DEFAULT_GROUP_ID;
                    TmpWonkaAttr.IsAudited = true;

                    TmpWonkaAttr.IsKey = KeyColNames.Contains(TmpWonkaAttr.AttrName);

                    NewImportSource.AddAttribute(TmpWonkaAttr);
                }

                if (NewImportSource.GetAttrCache().Count <= 0)
                    throw new WonkaBizRuleException(0, 0, "ERROR!  Could not import the schema because the Reader's field count was zero.");

                WonkaRefGroup NewImportGroup = new WonkaRefGroup();

                NewImportGroup.GroupId        = CONST_DEFAULT_GROUP_ID;
                NewImportGroup.GroupName      = psDatabaseTable;
                NewImportGroup.KeyTabCols     = KeyColNames;
                NewImportGroup.ProductTabName = psDatabaseTable;
                NewImportSource.AddGroup(NewImportGroup);

                WonkaRefSource GuestSource = new WonkaRefSource();

                GuestSource.SourceId   = 1;
                GuestSource.SourceName = "Guest";
                GuestSource.Status     = "Active";
                NewImportSource.AddSource(GuestSource);
            }
            else
                throw new WonkaBizRuleException(0, 0, "ERROR!  Could not import the schema for the database table.");

            PopulateDefaults();

            return NewImportSource;
        }

        public IMetadataRetrievable ImportSource(string psDatabaseTable, SqlConnection poDbConn)
        {
            bool bTableFound = false;

            WonkaImportSource NewImportSource = new WonkaImportSource();

            if ((poDbConn == null) || (poDbConn.State != ConnectionState.Open))
                throw new WonkaImportException("ERROR!  Provided connection is either null or is not open.");

            using (SqlCommand ValidateTableCmd = new SqlCommand(CONST_VALIDATE_TABLE_SQL, poDbConn))
            {
                ValidateTableCmd.CommandType = CommandType.Text;

                SqlParameter TableParam = new SqlParameter("@tname", SqlDbType.NVarChar, 128);
                TableParam.Value = psDatabaseTable;

                ValidateTableCmd.Parameters.Add(TableParam);

                using (SqlDataReader ValidateTableReader = ValidateTableCmd.ExecuteReader())
                {
                    if (ValidateTableReader.Read())
                        bTableFound = true;
                }
            }

            if (!bTableFound)
                throw new WonkaImportException("ERROR!  Table (" + psDatabaseTable + ") not found within the schema.");

            DataTable       TableSchema = null;
            HashSet<string> KeyAttrList = new HashSet<string>();

            // NOTE: Possible issues with ingesting the schema of a large table
            using (SqlCommand QueryTableCmd = poDbConn.CreateCommand())
            {
                QueryTableCmd.CommandText = "select * from " + psDatabaseTable;
                QueryTableCmd.CommandType = CommandType.Text;

                using (SqlDataReader QueryTableReader = QueryTableCmd.ExecuteReader(CommandBehavior.KeyInfo))
                {
                    TableSchema = QueryTableReader.GetSchemaTable();
                }
            }

            int nColumnCount = TableSchema.Columns.Count;
            foreach (DataRow TmpColInfoRow in TableSchema.Rows)
            {
                string sTmpColName = TmpColInfoRow["ColumnName"].ToString();
                string sTmpColType = TmpColInfoRow["DataType"].ToString();

                if (sTmpColType.Contains("System."))
                    sTmpColType = sTmpColType.Replace("System.", "");

                WonkaRefAttr TmpWonkaAttr = new WonkaRefAttr();

                TmpWonkaAttr.AttrId   = GenerateNewAttrId();
                TmpWonkaAttr.AttrName = sTmpColName;
                TmpWonkaAttr.ColName  = sTmpColName;
                TmpWonkaAttr.TabName  = psDatabaseTable;

                // TmpWonkaAttr.DefaultValue = TmpColInfoRow["DefaultValue"].ToString();
                // TmpWonkaAttr.Description  = (TmpCol.doc != null) ? TmpCol.doc.LongDescription : "";

                TmpWonkaAttr.IsDate    = IsTypeDate(sTmpColType);
                TmpWonkaAttr.IsNumeric = IsTypeNumeric(sTmpColType);
                TmpWonkaAttr.IsDecimal = IsTypeDecimal(sTmpColType);

                if (TmpWonkaAttr.IsNumeric || TmpWonkaAttr.IsDecimal)
                {
                    TmpWonkaAttr.Precision = Int32.Parse(TmpColInfoRow["NumericPrecision"].ToString());
                    TmpWonkaAttr.Scale     = Int32.Parse(TmpColInfoRow["NumericScale"].ToString());
                }
                else
                    TmpWonkaAttr.MaxLength = Int32.Parse(TmpColInfoRow["ColumnSize"].ToString());

                TmpWonkaAttr.FieldId   = TmpWonkaAttr.AttrId + 1000;
                TmpWonkaAttr.GroupId   = CONST_DEFAULT_GROUP_ID;
                TmpWonkaAttr.IsAudited = true;

                TmpWonkaAttr.IsKey = Boolean.Parse(TmpColInfoRow["IsKey"].ToString());
                if (TmpWonkaAttr.IsKey)
                    KeyAttrList.Add(sTmpColName);

                NewImportSource.AddAttribute(TmpWonkaAttr);                
            }

            if (NewImportSource.GetAttrCache().Count <= 0)
                throw new WonkaBizRuleException(0, 0, "ERROR!  Could not import the schema because the Reader's field count was zero.");

            WonkaRefGroup NewImportGroup = new WonkaRefGroup();

            NewImportGroup.GroupId        = CONST_DEFAULT_GROUP_ID;
            NewImportGroup.GroupName      = psDatabaseTable;
            NewImportGroup.KeyTabCols     = KeyAttrList;
            NewImportGroup.ProductTabName = psDatabaseTable;
            NewImportSource.AddGroup(NewImportGroup);

            return NewImportSource;
        }

        public IMetadataRetrievable ImportSource(HashSet<Type> poDataStructList)
        {
            WonkaImportSource NewAggregateSource = new WonkaImportSource();

            if (poDataStructList != null)
            {
                foreach (Type TempType in poDataStructList)
                {
                    IMetadataRetrievable TempSource = ImportSource(TempType);

                    TempSource.GetAttrCache().ForEach(x => NewAggregateSource.AddAttribute(x));
                    TempSource.GetGroupCache().ForEach(x => NewAggregateSource.AddGroup(x));
                }
            }

            return NewAggregateSource;
        }

        public IMetadataRetrievable ImportSource(Type poDataStructType, object poDataStructure = null)
        {
            WonkaImportSource NewImportSource = new WonkaImportSource();

            PropertyInfo[] Props = poDataStructType.GetProperties();

            foreach (PropertyInfo TmpProperty in Props)
            {
                Type   AttrType  = TmpProperty.PropertyType;
                string sAttrName = TmpProperty.Name;

                WonkaRefAttr TmpWonkaAttr = new WonkaRefAttr();

                TmpWonkaAttr.AttrId   = GenerateNewAttrId();
                TmpWonkaAttr.AttrName = sAttrName;
                TmpWonkaAttr.ColName  = sAttrName;
                TmpWonkaAttr.TabName  = poDataStructType.FullName;

                if (poDataStructure != null)
                {
                    object oTmpValue = TmpProperty.GetValue(poDataStructure);

                    TmpWonkaAttr.DefaultValue = Convert.ToString(oTmpValue);
                }

                TmpWonkaAttr.Description = "";

                TmpWonkaAttr.IsDate    = IsTypeDate(AttrType.Name);
                TmpWonkaAttr.IsNumeric = IsTypeNumeric(AttrType.Name);
                TmpWonkaAttr.IsDecimal = IsTypeDecimal(AttrType.Name);

                // NOTE: These values are simply defaults and have no real meaning
                if (TmpWonkaAttr.IsNumeric)
                {
                    TmpWonkaAttr.Precision = 9;
                    TmpWonkaAttr.Scale     = 0;
                }
                else if (TmpWonkaAttr.IsDecimal)
                {
                    TmpWonkaAttr.Precision = 9;
                    TmpWonkaAttr.Scale     = 9;
                }

                // TmpWonkaAttr.MaxLength = ?;

                TmpWonkaAttr.FieldId   = TmpWonkaAttr.AttrId + 1000;
                TmpWonkaAttr.GroupId   = CONST_DEFAULT_GROUP_ID;
                TmpWonkaAttr.IsAudited = true;

                TmpWonkaAttr.IsKey = (TmpWonkaAttr.AttrName.EndsWith("ID") || TmpWonkaAttr.AttrName.EndsWith("Id"));

                NewImportSource.AddAttribute(TmpWonkaAttr);
            }

            return NewImportSource;
        }

        public void PopulateDefaults()
        {
            // NOTE: Do work here
        }

        public static bool IsTypeDate(string psTypeName)
        {
            switch (psTypeName)
            {
                case "DateTime":
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsTypeDecimal(string psTypeName)
        {
            switch (psTypeName)
            {
                case "Float":
                case "Double":
                case "Decimal":
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsTypeNumeric(string psTypeName)
        {
            switch (psTypeName)
            {
                case "String":
                case "Guid":
                case "DateTime":
                    return false;

                case "Int32":
                    return true;

                case "Single":
                case "Double":
                    return true;

                default:
                    return false;
            }
        }

        #endregion

    }
}

