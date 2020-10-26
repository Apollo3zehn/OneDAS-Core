﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using OneDas.DataManagement.Database;
using OneDas.DataManagement.Explorer.Core;
using OneDas.Types;
using System;
using System.Net;
using System.Threading;
using static System.Net.WebRequestMethods;

namespace OneDas.DataManagement.Explorer.Controllers
{
    [Route("v1/api/data")]
    [ApiController]
    public class DataController : ControllerBase
    {
        #region Fields

        private ILogger _logger;
        private OneDasDatabaseManager _databaseManager;
        private OneDasExplorerStateManager _stateManager;

        #endregion

        public DataController(OneDasExplorerStateManager stateManager,
                                OneDasDatabaseManager databaseManager,
                                ILoggerFactory loggerFactory)
        {
            _stateManager = stateManager;
            _databaseManager = databaseManager;
            _logger = loggerFactory.CreateLogger("OneDAS Explorer");
        }

        /// <summary>
        /// Gets the requested data.
        /// </summary>
        /// <param name="campaignId">The campaign identifier.</param>
        /// <param name="variableId">The variable identifier.</param>
        /// <param name="datasetId">The dataset identifier.</param>
        /// <param name="begin">Start date/time.</param>
        /// <param name="end">End date/time.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>

        [HttpGet("{campaign-id}/variables/{variable-id}/datasets/{dataset-id}")]
        public IActionResult GetData(
            [FromRoute(Name = "campaign-id")] string campaignId,
            [FromRoute(Name = "variable-id")] string variableId,
            [FromRoute(Name = "dataset-id")] string datasetId,
            [BindRequired] DateTime begin,
            [BindRequired] DateTime end,
            CancellationToken cancellationToken)
        {
            campaignId = WebUtility.UrlDecode(campaignId);
            variableId = WebUtility.UrlDecode(variableId);
            datasetId = WebUtility.UrlDecode(datasetId);

            var remoteIpAddress = this.HttpContext.Connection.RemoteIpAddress;

            // log
            string userName;

            if (this.User.Identity.IsAuthenticated)
                userName = this.User.Identity.Name;
            else
                userName = "anonymous";

            DateTime.SpecifyKind(begin, DateTimeKind.Utc);
            DateTime.SpecifyKind(end, DateTimeKind.Utc);

            var message = $"User '{userName}' ({remoteIpAddress}) streams data: {begin.ToString("yyyy-MM-ddTHH:mm:ssZ")} to {end.ToString("yyyy-MM-ddTHH:mm:ssZ")} ...";
            _logger.LogInformation(message);

            try
            {
                _stateManager.CheckState();

                // dataset
                var path = $"{campaignId}/{variableId}/{datasetId}";

                if (!_databaseManager.Database.TryFindDataset(path, out var dataset))
                    return this.NotFound($"Could not find channel with name '{path}'.");

                var campaign = (CampaignInfo)dataset.Parent.Parent;

                // security check
                if (!Utilities.IsCampaignAccessible(this.User, campaign.Id, _databaseManager.Config.RestrictedCampaigns))
                    return this.Unauthorized($"The current user is not authorized to access the campaign '{campaign.Id}'.");

                // dataReader
                using var dataReader = dataset.IsNative ? _databaseManager.GetNativeDataReader(campaign.Id) : _databaseManager.GetAggregationDataReader();

                // read data
                var stream = dataReader.ReadAsStream(dataset, begin, end, 5 * 1000 * 1000UL);

                _logger.LogInformation($"{message} Done.");

                return this.File(stream, "application/octet-stream", "data.bin");
            }
            catch (Exception ex)
            {
                _logger.LogError($"{message} {ex.GetFullMessage()}");
                throw;
            }
        }
    }
}
