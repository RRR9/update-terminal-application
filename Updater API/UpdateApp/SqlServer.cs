using System.Data.SqlClient;
using System.Data;
using Newtonsoft.Json.Linq;

namespace UpdateApp
{
    public static class SqlServer
    {
        private static readonly string _connection = @"Data Source=(local);database=;Integrated Security=True;";

        public static string ExecuteQuery(string query)
        {
            string res = "";
            using (SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(query, _connection))
            {
                DataSet dataSet = new DataSet();
                sqlDataAdapter.Fill(dataSet);

                JObject jRes = new JObject();
                jRes["versions"] = (double)dataSet.Tables[0].Rows[0]["versions"];
                jRes["linkToDownload"] = dataSet.Tables[0].Rows[0]["linkToDownload"].ToString();
                res = jRes.ToString();
            }
            return res;
        }
    }
}
