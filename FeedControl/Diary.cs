using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace FeedControl
{
    class Diary
    {
        public Diary()
        {

        }

        public async Task updateDiary(string job, string cardNumber, string selectInstarName, string selectFeedName, 
            int selectFeedTime, int selectLastFeedTime, double measureFirstWeight, double measureFeedWeight, double measureTotalWeight)
        {
            try
            {
                string requestUrl = "https://ifactoryfarm.farminsf.com/back/createDiary/{job}/{cardNumber}"
                            .Replace("{job}", job)
                            .Replace("{cardNumber}", cardNumber);

                Console.WriteLine(requestUrl);

                JObject bodyMessage = new JObject();

    
                    JArray diaryItems = new JArray();

                    JObject feedName = new JObject();
                    JObject firstWeight = new JObject();
                    JObject feedWeight = new JObject();
                    JObject totalWeight = new JObject();
                    JObject feedTime = new JObject();
                    JObject lastFeedTime = new JObject();
                    JObject instar = new JObject();

                    //사료이름
                    feedName.Add("name", "feedName");
                    feedName.Add("value", selectFeedName);
                    diaryItems.Add(feedName);

                    //급이량
                    feedWeight.Add("name", "feedWeight");
                    feedWeight.Add("value", measureFeedWeight);
                    diaryItems.Add(feedWeight);

                    //사육상자 무게 (곤충 + 사료 상태에서 계측)
                    totalWeight.Add("name", "boxWeight");
                    totalWeight.Add("value", measureTotalWeight);
                    diaryItems.Add(totalWeight);

                    // 상자 내 사료 무게 (사육상자 무게 - 박스무게)
                    firstWeight.Add("name", "boxFeedWeight");
                    firstWeight.Add("value", measureTotalWeight - 2000);
                    diaryItems.Add(firstWeight);

                    //첫 밥
                    feedTime.Add("name", "feedTime");
                    feedTime.Add("value", selectFeedTime);
                    diaryItems.Add(feedTime);

                    //마지막 밥
                    lastFeedTime.Add("name", "lastfeedTime");
                    lastFeedTime.Add("value", selectLastFeedTime);
                    diaryItems.Add(lastFeedTime);

                    //생육단계
                    instar.Add("name", "instar");
                    instar.Add("value", selectInstarName);
                    diaryItems.Add(instar);

                    bodyMessage.Add("diaryItems", diaryItems);

           
                var client = new HttpClient();
                var response = await sendHttpRequestAsync(client, requestUrl, bodyMessage);

                // 응답 데이터 가져오기
                var responseJson = await response.Content.ReadAsStringAsync();

                // 응답 데이터 역직렬화
                var responseData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);

                JObject parseJSON = JObject.Parse(responseJson);

                var id = (int)parseJSON["diaryId"];

                //이미지 파일 업로드
                await uploadImageAsync(id);

             
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace.ToString());
            }
        }

        public async Task<HttpResponseMessage> sendHttpRequestAsync(HttpClient client, string requestUrl, JObject bodyMessage)
        {
            try
            {
                client.DefaultRequestHeaders.ExpectContinue = false; // <-- Make sure it is false.
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var httpContent = new StringContent(bodyMessage.ToString(Formatting.None), System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(requestUrl, httpContent);

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace.ToString());
                return null;
            }
        }

        public async Task uploadImageAsync(int diaryId)
        {
            try
            {
                var client = new HttpClient();

                using (var form = new MultipartFormDataContent())
                {
                    // Get the path of the local application data folder
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                    // Define the snapshot directory
                    string snapshotDirectory = Path.Combine(localAppData, "CAMERA_RFID", "Snapshot");

                    // Define the full file path
                    string imagePath = Path.Combine(snapshotDirectory, "Snapshot.bmp");

                    if (File.Exists(imagePath))
                    {
                        var imageContent = new ByteArrayContent(File.ReadAllBytes(imagePath));
                        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                        form.Add(imageContent, "image", Path.GetFileName(imagePath));

                        // API URL 생성
                        string apiUrlWithParams = "https://ifactoryfarm.farminsf.com/back/ifactory/api/file/uploadfile/{uploadPath}/{id}"
                            .Replace("{uploadPath}", "diary")
                            .Replace("{id}", diaryId.ToString());

                        // API 호출
                        var uploadResponse = await client.PostAsync(apiUrlWithParams, form);
                        if (uploadResponse.IsSuccessStatusCode)
                        {
                            var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
                            Console.WriteLine("image_upload_resut: " + uploadResult);
                        }
                        else
                        {
                            Console.WriteLine("Error: " + uploadResponse.StatusCode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace.ToString());
            }
        }
    }
}
