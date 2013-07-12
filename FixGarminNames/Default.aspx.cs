using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml;
using SuperRembo.GarminConnectClient;
using Newtonsoft.Json.Linq;


namespace FixGarminNames
{
    public partial class WebForm1 : System.Web.UI.Page
    {
        public List<String> outputList;
        static String getLocationName(String Lat, String Lon)
        {
            
            HttpWebRequest geonameRequest = WebRequest.CreateHttp(String.Format("http://api.geonames.org/extendedFindNearby?lat={0}&lng={1}&username={2}", Lat, Lon, ConfigurationManager.AppSettings["geonamesUsername"]));
            using (WebResponse geonameResponse = geonameRequest.GetResponse())
            using (StreamReader geonameReader = new StreamReader(geonameResponse.GetResponseStream()))
            {
                XmlDocument geonameXmlDoc = new XmlDocument();
                geonameXmlDoc.LoadXml(geonameReader.ReadToEnd());
                return String.Format("{0}/{1}", geonameXmlDoc.SelectSingleNode("//street").InnerText, geonameXmlDoc.SelectSingleNode("//placename").InnerText);
            }
        }

        static void updateActivity(String activityId, String newDescription, CookieContainer c)
        {
            String parameters = String.Format("{0}={1}", "value", HttpUtility.UrlEncode(newDescription));
            var request = (HttpWebRequest)WebRequest.Create(String.Format("http://connect.garmin.com/proxy/activity-service-1.2/json/name/{0}", activityId));
            request.CookieContainer = c;
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = parameters.Length;
            request.KeepAlive = true;
            request.Method = "POST";
            using (var writer = new StreamWriter(request.GetRequestStream()))
                writer.Write(parameters.ToString());
            using (WebResponse response = request.GetResponse())
                System.Diagnostics.Debug.WriteLine("Activity Updated");
            
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            outputList = new List<string>();
            SessionService garminSessionFactory = new SessionService();
            if (garminSessionFactory.SignIn(ConfigurationManager.AppSettings["garminConnectUsername"], ConfigurationManager.AppSettings["garminConnectPassword"]))
            {
                ActivitySearchService garminActivityFactory = new ActivitySearchService(garminSessionFactory.Session);
                List<SuperRembo.GarminConnectClient.Data.Activity> activities = garminActivityFactory.FindAllActivities();          
                System.Diagnostics.Debug.WriteLine("Total {0} activities found.", activities.Count);
                outputList.Add("Found " + activities.Count + " activities.");
			    foreach (var activity in activities)
			    {
                    if (activity.ActivityName == "Untitled")
                    {
                        Console.WriteLine("Activity {0}: {1}, {2}", activity.ActivityId, activity.ActivityName, activity.ActivityDescription);
                        SuperRembo.GarminConnectClient.Data.ActivitySummary summary = activity.ActivitySummary;
                        try
                        {
                            String detailUrl = String.Format("http://connect.garmin.com/proxy/activity-service-1.2/json/activity/{0}", activity.ActivityId);
                            HttpWebRequest request = WebRequest.CreateHttp(detailUrl);
                            request.CookieContainer = garminSessionFactory.Session.Cookies;
                            using (WebResponse response = request.GetResponse())
                            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                            {
                                JObject o = JObject.Parse(reader.ReadToEnd());
                                String newActivityName, locName;

                                try
                                {
                                    locName = getLocationName(o["activity"].SelectToken("activitySummaryBeginLatitude").SelectToken("value").ToString(), o["activity"].SelectToken("activitySummaryBeginLongitude").SelectToken("value").ToString());
                                }
                                catch
                                {
                                    locName = String.Empty;
                                }

                                if (locName.Length > 0)
                                    newActivityName = String.Format("{0} {1} ({2})", summary.BeginTimestamp.Value.ToString("yyyy-MMM-dd"), activity.ActivityType.Key, locName);
                                else
                                    newActivityName = String.Format("{0} {1}", summary.BeginTimestamp.Value.ToString("yyyy-MMM-dd"), activity.ActivityType.Key);

                                updateActivity(activity.ActivityId.ToString(), newActivityName, garminSessionFactory.Session.Cookies);
                                System.Diagnostics.Debug.WriteLine("new activity name: " + newActivityName);
                                outputList.Add("Updated activity with new title: " + newActivityName);

                            }

                        } catch (Exception err)
                        {
                            System.Diagnostics.Debug.WriteLine("FAILED: " + err.Message);
                        }

                    }
			    }

            } else {
                System.Diagnostics.Debug.WriteLine("Error logging in: " + garminSessionFactory.ToString());
            }
      
        

        }
    }
}