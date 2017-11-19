using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STVM.Stv.Data;
using STVM.Data;


namespace STVM.Stv.ApiV3
{
    class StvApiV3
    {
        public string Username
        {
            get { return savetv.Username; }
            set { savetv.Username = value; }
        }
        public string Password
        {
            get { return savetv.Password; }
            set { savetv.Password = value; }
        }

        private StvClientV3 savetv;

        public event LogEventHandler LogEvent
        {
            add { savetv.LogEvent += value; }
            remove { savetv.LogEvent -= value; }
        }

        public StvApiV3()
        {
            savetv = new StvClientV3();
        }

        public async Task<Response<string>> GetVersion()
        {
            return await savetv.GetRequest<string>("/v3/version", new QueryBuilder());
        }

        public async Task<Response<User>> GetUser()
        {
            QueryBuilder Query = new QueryBuilder()
            {
                {"fields", "contract.hasxlpackage,contract.hasxxlpackage,fullname,recordbuffer.followuptime,recordbuffer.leadtime" }
            };
            string Request = "/v3/user";
            return await savetv.GetRequest<User>(Request, Query);
        }

        public async Task<Response<List<TvCategory>>> GetTvCategories()
        {
            QueryBuilder Query = new QueryBuilder()
            {
                {"fields", "id, name" }
            };
            string Request = "/v3/tvcategories";
            return await savetv.GetRequest<List<TvCategory>>(Request, Query);
        }

        public async Task<Response<List<TvStation>>> GetTvStations()
        {
            QueryBuilder Query = new QueryBuilder()
            {
                {"fields", "id, isrecordable, largelogourl, name, smalllogourl" }
            };
            string Request = "/v3/tvstations";
            return await savetv.GetRequest<List<TvStation>>(Request, Query);
        }

        public async Task<Response<RecordCount>> GetRecordCount(IEnumerable<RecordStates> recordStates)
        {
            QueryBuilder Query = new QueryBuilder() {
                { "recordstates", String.Join(",", recordStates.Select(state => (int)state)) }
            };
            string Request = "/v3/records/count";
            return await savetv.GetRequest<RecordCount>(Request, Query);
        }

        public async Task<Response<List<Record>>> GetRecords(IEnumerable<RecordStates> recordStates, int from, int count)
        {
            QueryBuilder Query = new QueryBuilder() {
                { "recordstates", String.Join(",", recordStates.Select(state => (int)state)) },
                { "fields", Record.fieldsDefault },
                { "offset", from.ToString() },
                { "limit", count.ToString() },
            };
            string Request = "/v3/records";
            return await savetv.GetRequest<List<Record>>(Request, Query);
        }

        public async Task<Response<RecordDownloadUrl>> GetRecordDownloadUrl(int TelecastId, RecordFormats RecordFormat, bool AdFree)
        {
            QueryBuilder Query = new QueryBuilder() {
                { "adfree", AdFree.ToString() }
            };
            string Request = string.Format("/v3/records/{0}/downloads/{1}", TelecastId, (int)RecordFormat);
            return await savetv.GetRequest<RecordDownloadUrl>(Request, Query);
        }

        public async Task<Response<List<Telecast>>> GetTelecasts(TelecastQuery telecastQuery)
        {
            QueryBuilder Query = new QueryBuilder();
            Query.Add("fields", Telecast.fieldsDefault);
            Query.Add("limit", "2000");
            Query.Add("q", telecastQuery.searchText);
            Query.Add("searchtextscope", telecastQuery.searchFullText ? "1" : "2");
            Query.Add("exacttitle", telecastQuery.exactTitle);
            Query.Add("dayofweek", string.Join(",", telecastQuery.dayOfWeek));
            if (telecastQuery.tvStation > 0) Query.Add("tvstations", telecastQuery.tvStation.ToString());
            Query.Add("minstartdate", telecastQuery.minStartDate.ToUtcIsoString());
            Query.Add("maxstartdate", telecastQuery.maxStartDate.ToUtcIsoString());
            Query.Add("minstarttime", telecastQuery.minStartTime.ToUtcTimeString());
            Query.Add("maxstarttime", telecastQuery.maxStartTime.ToUtcTimeString());
            string Request = "/v3/telecasts";
            return await savetv.GetRequest<List<Telecast>>(Request, Query);
        }

        public async Task<Response<Telecast>> GetTelecastDetail(int TelecastId)
        {
            QueryBuilder Query = new QueryBuilder() {
                { "fields", Telecast.fieldsDetail }
            };
            string Request = string.Format("/v3/telecasts/{0}", TelecastId);
            return await savetv.GetRequest<Telecast>(Request, Query);
        }

        public async Task<Response<List<RecordResponse>>> PostRecord(IEnumerable<int> TelecastIds, int leadTime, int followUpTime)
        {
            PostRecordRequest Query = new PostRecordRequest() { 
                telecastIds = TelecastIds.ToList(),
                followUpTime = followUpTime,
                leadTime = leadTime
            };
            string Request = "/v3/records";
            return await savetv.PostRequest<List<RecordResponse>>(Request, Query);
        }

        public async Task<Response<List<RecordResponse>>> DeleteRecord(IEnumerable<int> TelecastIds)
        {
            QueryBuilder Query = new QueryBuilder() {
                { "telecastids", String.Join(",", TelecastIds) }
            };
            string Request = "/v3/records";
            return await savetv.DeleteRequest<List<RecordResponse>>(Request, Query);
        }
    }


}
