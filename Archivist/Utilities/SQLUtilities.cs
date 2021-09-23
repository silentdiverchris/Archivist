using Archivist.Classes;
using Archivist.Helpers;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivist.Utilities
{
    internal static class SQLUtilities
    {
        internal static List<SqlParameter> BuildSQLParameterList(
            string? name1 = null, object? value1 = null,
            string? name2 = null, object? value2 = null,
            string? name3 = null, object? value3 = null,
            string? name4 = null, object? value4 = null,
            string? name5 = null, object? value5 = null,
            string? name6 = null, object? value6 = null,
            string? name7 = null, object? value7 = null,
            string? name8 = null, object? value8 = null,
            string? name9 = null, object? value9 = null,
            bool ignoreNullValues = true)
        {
            List<SqlParameter> parameters = new();

            if (value1 != null || !ignoreNullValues)
                parameters.Add(new SqlParameter(name1, value1));

            if (name2 != null)
            {
                if (value2 != null || !ignoreNullValues)
                    parameters.Add(new SqlParameter(name2, value2));

                if (name3 != null)
                {
                    if (value3 != null || !ignoreNullValues)
                        parameters.Add(new SqlParameter(name3, value3));

                    if (name4 != null)
                    {
                        if (value4 != null || !ignoreNullValues)
                            parameters.Add(new SqlParameter(name4, value4));

                        if (name5 != null)
                        {
                            if (value5 != null || !ignoreNullValues)
                                parameters.Add(new SqlParameter(name5, value5));

                            if (name6 != null)
                            {
                                if (value6 != null || !ignoreNullValues)
                                    parameters.Add(new SqlParameter(name6, value6));

                                if (name7 != null)
                                {
                                    if (value7 != null || !ignoreNullValues)
                                        parameters.Add(new SqlParameter(name7, value7));

                                    if (name8 != null)
                                    {
                                        if (value8 != null || !ignoreNullValues)
                                            parameters.Add(new SqlParameter(name8, value8));

                                        if (name9 != null)
                                        {
                                            if (value9 != null || !ignoreNullValues)
                                                parameters.Add(new SqlParameter(name9, value9));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return parameters;
        }

        /// <summary>
        /// Generate the SQL text that replicates this proc call, to be logged. The result should be 
        /// pasteable into SSMS and run as-is
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        internal static string GenerateStoredProcedureCallText(string name, List<SqlParameter> parameters)
        {
            StringBuilder sb = new(500);
            sb.Append($"Exec {name}");

            if (parameters != null && parameters.Any())
            {
                foreach (SqlParameter param in parameters)
                {
                    if (param.Value is not null)
                    {
                        if (param.SqlDbType == System.Data.SqlDbType.VarChar || param.SqlDbType == System.Data.SqlDbType.NVarChar || param.SqlDbType == System.Data.SqlDbType.DateTime || param.SqlDbType == System.Data.SqlDbType.Date || param.SqlDbType == System.Data.SqlDbType.DateTime2)
                        {
                            sb.Append($" @{param.ParameterName}='{param.Value.ToString()!.TruncateWithEllipsis(100)}',");
                        }
                        else
                        {
                            sb.Append($" @{param.ParameterName}={param.Value},");
                        }
                    }
                    else
                    {
                        sb.Append($" @{param.ParameterName}=NULL,");
                    }
                }

                if (parameters.Any())
                    sb.Remove(sb.Length - 1, 1);
            }

            return sb.ToString();
        }

        internal static async Task<Result> ExecuteStoredProcedureNonQueryAsync(string connectionString, string name, List<SqlParameter> parameters)
        {
            Result result = new("ExecuteStoredProcedureNonQueryAsync", false);

            try
            {
                if (parameters == null)
                {
                    parameters = new List<SqlParameter>();
                }

                using (SqlConnection conn = new(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandText = name;
                    cmd.Parameters.AddRange(parameters.ToArray());
                    cmd.CommandTimeout = Constants.DB_TIMEOUT_SECONDS;
                    await cmd.ExecuteNonQueryAsync();
                    await conn.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                string storedProcCall = SQLUtilities.GenerateStoredProcedureCallText(name, parameters);
                result.AddException(ex);
                result.AddError(storedProcCall);
            }

            if (result.HasErrors)
            {
                result.AddError($"Executing {name} failed");
            }

            return result;
        }
    }
}

