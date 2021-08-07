using Archivist.Classes;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivist.Helpers
{
    internal static class StoredProcedureHelpers
    {
        internal static List<SqlParameter> BuildSQLParameterList(
            string name1 = null, object value1 = null,
            string name2 = null, object value2 = null,
            string name3 = null, object value3 = null,
            string name4 = null, object value4 = null,
            string name5 = null, object value5 = null,
            string name6 = null, object value6 = null,
            string name7 = null, object value7 = null,
            string name8 = null, object value8 = null,
            string name9 = null, object value9 = null,
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
                            sb.Append($" @{param.ParameterName}='{param.Value.ToString().TruncateWithEllipsis(100)}',");
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

        //internal static async Task<Result> ExecuteStoredProcedureQueryRecordListAsync(
        //    string connectionString,
        //    int callingUserAccountId,
        //    string name,
        //    List<SqlParameter> parameters = null,
        //    bool errorIfNoRecords = true,
        //    int? takeFirst = null)
        //{
        //    Result result = new();

        //    try
        //    {
        //        if (parameters == null)
        //        {
        //            parameters = new List<SqlParameter>();
        //        }

        //        if (!parameters.Any(_ => _.ParameterName == "CallingUserAccountId"))
        //        {
        //            parameters.Add(new SqlParameter("CallingUserAccountId", callingUserAccountId));
        //        }

        //        if (takeFirst != null)
        //        {
        //            parameters.Add(new SqlParameter("TakeFirst", (int)takeFirst));
        //        }

        //        using (result = await ExecuteStoredProcedureQueryAsync(
        //            connectionString: connectionString,
        //            callingUserAccountId: callingUserAccountId,
        //            name: name,
        //            parameters: parameters,
        //            errorIfNoRecords: errorIfNoRecords))
        //        {
        //            if (result.DataReader.HasRows)
        //            {
        //                result.ReturnedRecordset = new List<List<object>>();

        //                while (await result.DataReader.ReadAsync())
        //                {
        //                    object[] values = new object[result.DataReader.FieldCount];

        //                    result.DataReader.GetValues(values);

        //                    result.ReturnedRecordset.Add(values.ToList());
        //                }
        //            }
        //            else
        //            {
        //                result.ReturnedRecordset = new List<List<object>>();

        //                if (errorIfNoRecords)
        //                    result.SetErrorTitle("ExecuteStoredProcedureQueryRecordList found no values, which is flagged as an error");
        //            }

        //            await result.DataReader.CloseAsync();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        string storedProcCall = StoredProcedureHelpers.GenerateStoredProcedureCallText(name, parameters);
        //        result.Exception = ex;
        //        result.AddError(storedProcCall);
        //    }

        //    return result;
        //}

        //internal static async Task<Result> ExecuteStoredProcedureQueryRecordAsync(
        //    string connectionString,
        //    int callingUserAccountId,
        //    string name,
        //    List<SqlParameter> parameters,
        //    bool errorIfNoRecords)
        //{
        //    Result result = new();

        //    try
        //    {
        //        using (result = await ExecuteStoredProcedureQueryAsync(
        //            connectionString: connectionString,
        //            callingUserAccountId: callingUserAccountId,
        //            name: name,
        //            parameters: parameters,
        //            errorIfNoRecords: errorIfNoRecords))
        //        {
        //            if (result.OK)
        //            {
        //                if (result.DataReader.HasRows)
        //                {
        //                    await result.DataReader.ReadAsync();

        //                    object[] values = new object[result.DataReader.FieldCount];

        //                    result.DataReader.GetValues(values);

        //                    result.ReturnedRecord = values.ToList();
        //                }
        //                else
        //                {
        //                    if (errorIfNoRecords)
        //                        result.SetErrorTitle("ExecuteStoredProcedureQueryRecordAsync found no values, which is flagged as an error");
        //                }

        //                await result.DataReader.CloseAsync();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        string storedProcCall = StoredProcedureHelpers.GenerateStoredProcedureCallText(name, parameters);
        //        result.Exception = ex;
        //        result.AddError(storedProcCall);
        //    }

        //    return result;
        //}

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
                string storedProcCall = StoredProcedureHelpers.GenerateStoredProcedureCallText(name, parameters);
                result.AddException(ex);
                result.AddError(storedProcCall);
            }

            if (result.HasErrors)
            {
                result.AddError($"Executing {name} failed");
            }

            return result;
        }

        //internal static Result ExecuteStoredProcedureNonQuery(string connectionString, string name, List<SqlParameter> parameters)
        //{
        //    Result result = ExecuteStoredProcedureNonQueryAsync(connectionString, name, parameters);

        //    return result;
        //}

        /// <summary>
        /// Intentionally synchronous, used exclusively by the UserConfiguration service, which is synchronous, for now.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        /// <param name="callingUserId"></param>
        /// <param name="errorIfNoRecords"></param>
        /// <returns></returns>
        //internal static Result ExecuteStoredProcedureQuery(
        //    string connectionString,
        //    int callingUserAccountId,
        //    string name,
        //    List<SqlParameter> parameters,
        //    bool errorIfNoRecords)
        //{
        //    Result result = new();

        //    try
        //    {
        //        if (!parameters.Any(_ => _.ParameterName == "CallingUserAccountId"))
        //        {
        //            parameters.Add(new SqlParameter("CallingUserAccountId", callingUserAccountId));
        //        }

        //        result.SqlConnection = new SqlConnection(connectionString);

        //        result.SqlConnection.Open();

        //        SqlCommand cmd = result.SqlConnection.CreateCommand();
        //        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        //        cmd.CommandText = name;

        //        foreach (SqlParameter param in parameters)
        //        {
        //            cmd.Parameters.Add(param);
        //        }

        //        result.DataReader = cmd.ExecuteReader();

        //        if (errorIfNoRecords && !result.DataReader.HasRows)
        //        {
        //            throw new Exception($"{name} returned no rows, which is flagged as an exception");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        string storedProcCall = StoredProcedureHelpers.GenerateStoredProcedureCallText(name, parameters);
        //        result.Exception = ex;
        //        result.AddError(storedProcCall);
        //    }

        //    if (result.Fail)
        //    {
        //        result.ErrorTitle = $"Executing {name} failed";
        //    }

        //    return result;
        //}

        //internal static async Task<Result> ExecuteStoredProcedureQueryAsync(
        //    string connectionString,
        //    int callingUserAccountId,
        //    string name,
        //    List<SqlParameter> parameters,
        //    bool errorIfNoRecords)
        //{
        //    Result result = new();

        //    try
        //    {
        //        if (parameters is null)
        //            parameters = new List<SqlParameter>();

        //        if (!parameters.Any(_ => _.ParameterName == "CallingUserAccountId"))
        //        {
        //            parameters.Add(new SqlParameter("CallingUserAccountId", callingUserAccountId));
        //        }

        //        result.SqlConnection = new SqlConnection(connectionString);

        //        result.SqlConnection.Open();

        //        SqlCommand cmd = result.SqlConnection.CreateCommand();
        //        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        //        cmd.CommandText = name;

        //        foreach (SqlParameter param in parameters)
        //        {
        //            cmd.Parameters.Add(param);
        //        }

        //        result.DataReader = await cmd.ExecuteReaderAsync();

        //        if (errorIfNoRecords && !result.DataReader.HasRows)
        //        {
        //            throw new Exception($"{name} returned no rows, which is flagged as an exception");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        string storedProcCall = StoredProcedureHelpers.GenerateStoredProcedureCallText(name, parameters);
        //        result.Exception = ex;
        //        result.AddError(storedProcCall);
        //    }

        //    if (result.Fail)
        //    {
        //        result.ErrorTitle = $"Executing {name} failed";
        //    }

        //    return result;
        //}
    }
}

