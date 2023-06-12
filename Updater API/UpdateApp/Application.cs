using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System;
using log4net;

namespace UpdateApp
{
    [Route("appversion")]
    public class Application: ControllerBase
    {
        private readonly string _password = "";
        private static readonly ILog _log = LogManager.GetLogger(typeof(Application));

        [HttpGet]
        public IActionResult CheckVersion(int platform, int typeApplication, string hash)
        {
            _log.Info($"Request: route?platform={platform}&typeApplication={typeApplication}&hash={hash}");
            if(!CreateMd5Hash(platform.ToString() + typeApplication.ToString() + _password).Equals(hash)) {
                return BadRequest();
            }
            string sqlQuery = $"SELECT * FROM [DB].[dbo].[INFO_VERSION] WHERE [statusNew] = 1 AND [PlatformsID] = {platform} AND [TypeApplicationID] = {typeApplication}";
            string response = "";
            try
            {
                response = SqlServer.ExecuteQuery(sqlQuery);
            }
            catch(Exception ex)
            {
                _log.Error("Method _ExecuteQuery_", ex);
                return BadRequest();
            }
            _log.Info($"Response: {response}");
            return Ok(response);
        }

        private string CreateMd5Hash(string input)
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
