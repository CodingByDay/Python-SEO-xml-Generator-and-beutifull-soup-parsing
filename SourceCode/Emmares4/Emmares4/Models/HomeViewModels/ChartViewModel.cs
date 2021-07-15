using Dapper;
using Emmares4.Data;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Emmares4.Models.HomeViewModels
{
    public class ChartViewModel
    {
        public ChartInterval Interval { get; set; }
        public List<string> XLables { get; set; }
        public List<double> Values { get; set; }
        public string RatedEmails { get; set; }
        public string AverageScore { get; set; }
        public string CommunityMatch { get; set; }
        public string Evaluators { get; set; }

        private Dictionary<ChartInterval, int> IntervalDays = new Dictionary<ChartInterval, int>()
        {
            { ChartInterval.Week, 7 },
            { ChartInterval.Month, 30 },
            { ChartInterval.Year, 365 }
        };

        DbConnection _dbConnection;
        public ChartViewModel(ApplicationDbContext context, DbConnection dbConnection, ApplicationUser user, bool IsMarketeer, ChartInterval interval)
        {
            Interval = interval;

            _dbConnection = dbConnection;

            string chartSP = "GetChartData", chartStatsSP = "GetChartStats";

            if(IsMarketeer)
            {
                chartSP = "Marketeer_GetChartData";
                chartStatsSP = "Marketeer_GetChartStats";
            }

            var data = _dbConnection.Query(chartSP, new { chartType = (int)interval, user = user.Id },
                null, true, null, System.Data.CommandType.StoredProcedure)
                .ToDictionary(x=>x.Label, x=>x.Ratings);

            XLables = data.Keys.Cast<string>().ToList();
            Values = data.Values.Cast<double>().ToList();

            var stats = _dbConnection.Query(chartStatsSP, new { chartType = (int)interval, user = user.Id },
               null, true, null, System.Data.CommandType.StoredProcedure);

            if (stats.Any())
            {
                var stat = stats.First();
                RatedEmails = stat.RatedEmails.ToString();
                AverageScore = stat.AverageScore.ToString();
                CommunityMatch = stat.CommunityMatch?.ToString();
                Evaluators = stat.Evaluators?.ToString();
            }
        }
    }

    public enum ChartInterval
    {
        Daily,
        Week,
        Month,
        Year
    }
}
