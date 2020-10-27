using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Core;

namespace GpeDatalakeAPI
{
    public class DatalakeExporter
    {

        private readonly HttpClient _client;
        private const string ApikeyLocation = "/apikey";
        private static string currentApiKey = "";
        private string environment;
        private string account;
        private string password;
        private string baseUrl;

        private string _batchId = "GPE";
        private string _batchRunId;
        private string _datetime;
        private int _retry = 0;
        private int _batchV = 1;
        private string _summary = "";

        public DatalakeExporter(IConfigurationRoot configuration, string env)
        {
            environment = configuration.GetSection($"Settings:{env}:env").Value;
            account = configuration.GetSection($"Settings:{env}:dataLakeAccount").Value;
            password = configuration.GetSection($"Settings:{env}:dataLakePassword").Value;
            baseUrl = configuration.GetSection($"Settings:{env}:dataLakeBase").Value;

            _client = new HttpClient(new HttpClientHandler()) { BaseAddress = new Uri(baseUrl) };

            Log.Debug($"env: {environment}");
            Log.Debug($"account: {account}");
            Log.Debug($"password: {password}");
            Log.Debug($"baseUrl: {baseUrl}");
            Log.Debug($"----------------------------");
        }

        public async Task RunDataLakeExporter(string env)
        {
            _datetime = $"{DateTime.UtcNow:s}";
            _batchRunId = $"{_batchId}-{DateTime.UtcNow:yyyy-MM-dd}-v{_batchV}";
            await DataLakeExport(env, 1);
        }

        private async Task DataLakeExport(string env, int days, int startDays = 0)
        {
            Log.Debug($"starting @ {DateTime.UtcNow:R}\r");

            Console.WriteLine($"...");

            if (await GetApiKeyFromCredentials())
            {
                doOver: // loop point

                _batchRunId = _batchRunId.Substring(0, _batchRunId.LastIndexOf("_-_", StringComparison.Ordinal) + 3);

                if (await BatchStartStop(true, "ID"))
                {
                    await ProcessData("index", days, startDays, "ID");

                    Thread.Sleep(5 * 1000);
                    await BatchStartStop(false, "ID");
                }
                else
                {
                    Thread.Sleep(30000);
                    _retry++;

                    if (_retry < 5)
                    {
                        Log.Debug($"Failed attempt {_retry}... re-trying @ {DateTime.UtcNow:s}");
                        goto doOver;
                    }
                    else
                    {
                        Log.Warning("DataLake erred too many times, I had to quit retrying after 5 attempts @ 30s spacing...");
                    }
                }

                await SendLogout();
            }

        }

        private async Task<string> SendLogout()
        {
            var keys = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("apikey", currentApiKey),
                new KeyValuePair<string, string>("format", "JSON"),
            };
            var body = new FormUrlEncodedContent(keys);

            var result = await _client.PostAsync("/apikey/logout", body);
            currentApiKey = "";
            if (result.IsSuccessStatusCode)
            {
                Log.Information($"logged out at {DateTime.UtcNow}");
             
                return await result.Content.ReadAsStringAsync();
            }

            return "";
        }

        readonly DefaultContractResolver contractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        };



        private async Task<bool> SendData(List<DataPoint> data)
        {
            var ok = true;
            var skipperVal = 900; // max batch size is 1000, but we had issues over 900... so leave as 900 per batch
            
            try
            {
                var retriesCount = 0;
                var runs = (int)(data.Count / skipperVal) + 1;

                var parallelLoopResult = Parallel.For(0, runs, new ParallelOptions() { MaxDegreeOfParallelism = 3 }, async i =>
                {
                    var d = data.Skip(i * skipperVal).Take(skipperVal);

                    if (!d.Any()) return;

                    Retrier: // Loop point for retries

                    var json = JsonConvert.SerializeObject(d.ToList(),
                        Formatting.None, new JsonSerializerSettings()
                        {
                            NullValueHandling = NullValueHandling.Ignore,
                            DefaultValueHandling = DefaultValueHandling.Ignore,
                            ContractResolver = contractResolver,
                            DateFormatString = "yyyy-MM-ddTHH:mm:ss",
                            MaxDepth = 5,
                            Formatting = Formatting.None
                        });

                    var body = new MultipartFormDataContent("-x-x-x-x-x-")
                       {
                        {
                            new StringContent(currentApiKey, Encoding.UTF8),
                            "apikey"
                        },
                        {
                            new StringContent("1", Encoding.UTF8),
                            "dictionaryVersion"
                        },
                        {
                            new StringContent("JSON"),
                            "dataFormat"
                        },
                        {
                            new StringContent(DateTime.UtcNow.Ticks.ToString() + "-" + i),
                            "requestId"
                        },
                        {
                            new StringContent(_batchRunId, Encoding.UTF8),
                            "batchRunId"
                        },
                        {
                            new StringContent(json, Encoding.UTF8),
                            "data"
                        }
                       };

                    try
                    {
                        var result = await _client.PostAsync($"/marketdashboard/DataPoint", body);

                        if (!result.IsSuccessStatusCode)
                        {
                            var error = await result.Content.ReadAsStringAsync();
                            Log.Debug($"{error}\n");
                            Log.Error(error);
                            ok = false;
                            //throw new Exception("end");
                        }

                        Console.Write("+");
                    }
                    catch (Exception)
                    {
                        retriesCount++;
                        Thread.Sleep(2000);
                        Log.Debug($"Retrying {_batchRunId}");
                        if (retriesCount < 4) goto Retrier;
                    }
                });

                if (parallelLoopResult.IsCompleted)
                {
                    System.Threading.Thread.SpinWait(1000);
                    return await Task.FromResult(ok);
                }
            }
            catch (Exception e)
            {
                Log.Debug($"{e.Message} {e.StackTrace}");
                Log.Error(e.Message);

            }
            return await Task.FromResult(false);
        }

        private async Task<bool> BatchStartStop(bool starting = true, string product = "")
        {

            var match = new Regex(@"[a-z ]").Replace("", "");

            var body = new FormUrlEncodedContent(
                new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("eventType", starting ? "Start" : "End"),
                    new KeyValuePair<string, string>("dictionaryVersion", "1"),
                    new KeyValuePair<string, string>("batchId", $"{_batchId}_{match}"),
                    new KeyValuePair<string, string>("asOf", _datetime),
                    new KeyValuePair<string, string>("runDate", $"{DateTime.Parse(_datetime):yyyy-MM-ddT02:00:00.000}"),
                    new KeyValuePair<string, string>("apikey", currentApiKey)
                });

            var result = await _client.PostAsync($"/marketdashboard/BatchRun/{_batchRunId}/Events", body);
            if (result.IsSuccessStatusCode)
            {
                Log.Information($"{(starting ? "Started" : "Stopped")} batch {_batchRunId}");
                return true;
            }
            else
            {
                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    _batchV++;
                    if (_batchRunId.Contains("backfill"))
                    {
                        _batchRunId = _batchRunId.Replace($"-v{_batchV - 1}-", $"-v{_batchV}-");
                    }
                    else
                    {
                        _batchRunId = $"{_batchId}-{DateTime.UtcNow:yyyy-MM-dd}-v{_batchV}";
                    }

                    return BatchStartStop(starting, product).Result;
                }
            }

            Log.Debug($"{await result.Content.ReadAsStringAsync()}\n");

            return false;
        }


        private async Task<List<DataPoint>> ProcessData(string index, int days = 1, int startDays = 0, string productIncoming = "")
        {
            // TODO: Load your data here
            // Add your datasource GET in here
            // for now i'll use a list of DataPoint with one entry'
            var data = new List<DataPoint>()
            {
                new DataPoint("id", "category", "concept", "macroRegion", "region", 2020, 1, "unit", 22.0f, "vintage")
            };
 
            if (data.Any())
            {
                if (await SendData(data))
                {
                    Log.Information($"\t sent {index} {data.Count}");

                    Log.Debug($"DataLake send {index} - {data.Count} dp @ {DateTime.UtcNow:T}\r");

                }

                return data;
            }

            return null;
        }

        private async Task<bool> GetApiKeyFromCredentials()
        {
            var keys = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("username", account),
                new KeyValuePair<string, string>("password", password),
            };
            var body = new FormUrlEncodedContent(keys);

            var result = await _client.PostAsync(ApikeyLocation, body);

            if (result.IsSuccessStatusCode)
            {
                currentApiKey = await result.Content.ReadAsStringAsync();
                return true;
            }
            else
            {
                var resu = await result.Content.ReadAsStringAsync();
                Log.Debug($"{resu}");
            }

            return false;
        }
    }
}
