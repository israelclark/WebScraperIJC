using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;


namespace WebScraperIJC
{
    internal class MyRecord
    {
        internal string Name;
        internal string DOB;
        internal string Gender;
        internal string NumSO;
        internal List<string> Charges;

        public MyRecord(string numSO, string name, string gender, string DOB, List<string> charges)
        {
            this.NumSO = numSO;
            this.Name = name;
            this.Gender = gender;
            this.DOB = DOB;
            this.Charges = charges;
        }

    }

    public class MyScraper
    {

        private HttpWebRequest request;
        private Stream dataStream;
        private string status;
        private string title = null;

        public String Status
        {
            get
            {
                return status;
            }
            set
            {
                status = value;
            }
        } 
        
        public String Title
        {
            get
            {
                return title;
            }
            set
            {
                title = value;
            }
        }

        public MyScraper(string url)
        {
            request = (HttpWebRequest)HttpWebRequest.Create(url);
        }

        public MyScraper(string url, string method) : this(url)
        {

            if (method.Equals("GET") || method.Equals("POST"))
            {                
                request.Method = method;
            }
            else
            {
                throw new Exception("Invalid Method Type");
            }
        }

        public MyScraper(string url, string method, string data) : this(url, method)
        {
            // Create POST data and convert it to a byte array.
            string postData = data;
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);

            // Set the ContentType property of the WebRequest.
            request.ContentType = "application/x-www-form-urlencoded";

            // Set the ContentLength property of the WebRequest.
            request.ContentLength = byteArray.Length;

            // Get the request stream.
            dataStream = request.GetRequestStream();

            // Write the data to the request stream.
            dataStream.Write(byteArray, 0, byteArray.Length);

            // Close the Stream object.
            dataStream.Close();

        }

        public string GetResponse()
        {
            // Get the original response.
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            this.Status = response.StatusDescription;            

            // Get the stream containing all content returned by the requested server.
            dataStream = response.GetResponseStream();

            // Open the stream using a StreamReader for easy access.
            StreamReader reader = new StreamReader(dataStream);

            // Get the page title
            char[] buffer = new char[256];
            int count = reader.Read(buffer, 0, 256);
            while (count > 0 && title == null)
            {
                string outputData = new string(buffer, 0, count);
                Match match = Regex.Match(outputData, @"<title>([^<]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    this.Title = match.Groups[1].Value;
                }
            }

            // Read the content fully up to the end to get data.
            string responseFromServer = reader.ReadToEnd();

            // Clean up the streams.
            reader.Close();
            dataStream.Close();
            response.Close();

            return responseFromServer;
        }

        public static void Main()
        {
            // VARIABLE DECLARATION
            string filename = "Jail Roster - ";
            string domainURL = @"http://www.mctx.org/mcso/";
            string searchURL = domainURL + "JailRosterSearch.asp";
            string lastName = "B";

            string columnSeperator = "|";
            string columns = "SO#" + columnSeperator + "Name" + columnSeperator + "Gender" + columnSeperator + "DOB" + columnSeperator + "Charges";

            string soPrompt = "SO#:";
            string namePrompt = "Name:";
            string genderPrompt = "Gender:";
            string dobPrompt = "DOB:";
            string chargesPrompt = "Charge:";

            string regexLinks = @"(<a.*?>.*?</a>)";
            string regexTable = @"(<TD.*?>.*?$)"; // |
            string regexParseCodes = @"(<TD.*?>.*?</TD>)";
            
             
            // Request page code on searchURL
            MyScraper myRequest = new MyScraper(searchURL, "POST", "a=value1&b=value2");
            
            // Get response string
            string response = myRequest.GetResponse().ToString();

            // Setup txt file name
            filename += myRequest.Title + ".txt";
                       
            //parse all links from response            
            List<string> newHrefs = new List<string>();
            MatchCollection matches = Regex.Matches(response, regexLinks, RegexOptions.IgnoreCase);            
            if (matches.Count > 0)
            {                
                foreach(Match match in matches)
                {
                    foreach (Group m in match.Groups)
                    {                        
                        if (!newHrefs.Contains(m.Value)) newHrefs.Add(m.Value);
                    }
                }
            }

            // record all href & name info where last name matches
            List<List<string>> matchingPages = new List<List<string>>();
            foreach (string newHref in newHrefs)
            { 
                // parse the link into name & link
                string[] temp = newHref.Split(new string[] { "<a href=", "<A href=", "</A>", ">" }, StringSplitOptions.RemoveEmptyEntries);
                if (temp[1].StartsWith(lastName))
                {
                    List<string> newMatch = new List<string>();
                    newMatch.Add(temp[1]); // full name
                    newMatch.Add(domainURL + temp[0]); // href
                    matchingPages.Add(newMatch);                    
                }                
            }            

            // Create randomizer to stagger scrapes
            Random random = new Random();
            int wait = random.Next(1000, 5000);
                        
            List<MyRecord> matchingRecords = new List<MyRecord>();
            foreach(List<string> page in matchingPages)
            {                   
                wait = random.Next(1000, 5000);
                Console.WriteLine("About to sleep for {0}ms.", wait);
                System.Threading.Thread.Sleep(wait);
                
                
                // scrape record page
                myRequest = new MyScraper(page[1], "POST", "a=value1&b=value2");
                string recordResponse = myRequest.GetResponse().ToString();

                //parse relevant data
                matches = Regex.Matches(recordResponse, regexTable, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
                if (matches.Count > 0)
                {
                    string thisName = "";
                    string thisSO = "";
                    string thisGender = "";
                    string thisDOB = "";
                    List<string> theseCharges = new List<string>();                    

                    foreach (Match match in matches)
                    {
                        string row = match.Groups[0].Value;                        
                        if (row.Contains(namePrompt))
                        {
                            matches = Regex.Matches(row, regexParseCodes, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
                            foreach (Match thisMatch in matches)
                            {
                                foreach (Group m in thisMatch.Groups)
                                {
                                    thisName = StripTagsCharArray(m.Value);
                                    thisName = thisName.Trim();
                                    if (thisName != namePrompt) break;
                                }
                            }                            
                        }
                        else if (row.Contains(chargesPrompt))
                        {
                            string thisCharge = null;
                            matches = Regex.Matches(row, regexParseCodes, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
                            foreach (Match thisMatch in matches)
                            {
                                foreach (Group m in thisMatch.Groups)
                                {
                                    thisCharge = StripTagsCharArray(m.Value);
                                    thisCharge = thisCharge.Trim();
                                    if (thisCharge != chargesPrompt)
                                    {
                                        theseCharges.Add(thisCharge);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (row.Contains(soPrompt))
                        {
                            matches = Regex.Matches(row, regexParseCodes, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
                            foreach (Match thisMatch in matches)
                            {
                                foreach (Group m in thisMatch.Groups)
                                {
                                    thisSO = StripTagsCharArray(m.Value);
                                    thisSO = thisSO.Trim();
                                    if (thisSO != soPrompt) break;
                                }
                            }                            
                        }
                        else if (row.Contains(genderPrompt))
                        {
                            matches = Regex.Matches(row, regexParseCodes, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
                            foreach (Match thisMatch in matches)
                            {
                                foreach (Group m in thisMatch.Groups)
                                {
                                    thisGender = StripTagsCharArray(m.Value);
                                    thisGender = thisGender.Trim();
                                    if (thisGender != genderPrompt) break;
                                }
                            } 
                        }
                        else if (row.Contains(dobPrompt))
                        {
                            matches = Regex.Matches(row, regexParseCodes, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
                            foreach (Match thisMatch in matches)
                            {
                                foreach (Group m in thisMatch.Groups)
                                {
                                    thisDOB = StripTagsCharArray(m.Value);
                                    thisDOB = thisDOB.Trim();
                                    if (thisDOB != dobPrompt) break;                                    
                                }
                            }
                        }
                        
                    }
                    MyRecord thisRecord = new MyRecord(thisSO, thisName, thisGender, thisDOB, theseCharges);
                    matchingRecords.Add(thisRecord);
                }                
            }

            // create output string
            string output = columns + "\n";
            foreach (MyRecord record in matchingRecords)
            {
                output += record.NumSO  + columnSeperator +
                          record.Name   + columnSeperator +
                          record.Gender + columnSeperator +
                          record.DOB    + columnSeperator;

                foreach(string charge in record.Charges)
                {
                    output += charge  + columnSeperator;
                }
                output += "\n";
            }
            
            // Write data to file 
            string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                         
            using (StreamWriter outfile = new StreamWriter(mydocpath + @"\" + filename, false))
            {
                //outfile.Write(response);
                outfile.Write(output);
            }
            

        }

        public static string StripTagsCharArray(string source)
        {
            char[] array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char let = source[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }
            string temp = new string(array, 0, arrayIndex);
            temp.Trim();
            return temp;
        }
    }
}
