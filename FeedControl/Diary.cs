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

        public async Task post(string job, string cardNumber, double measureCO2, double measureNH3, double measureTEMP, double measureHUMID)
        {


            try
            {
                string note = "";
                string requestUrl = "https://ifactoryfarm.farminsf.com/back/createDiary/{job}/{cardNumber}"
                            .Replace("{job}", job)
                            .Replace("{cardNumber}", cardNumber);

                Console.WriteLine(requestUrl);

                note = "사육상자 온도 : " + measureTEMP;
                note = note + "\n사육상자 습도 : " + measureHUMID;
                note = note + "\n사육상자 CO2 : " + measureCO2;
                note = note + "\n사육상자 NH3 : " + measureNH3;

                JObject bodyMessage = new JObject();
                bodyMessage.Add("note", note);

                JArray tags = new JArray();

                JObject tagCO2 = new JObject();
                JObject tagNH3 = new JObject();
                JObject tagHUMID = new JObject();
                JObject tagTEMP = new JObject();

                tagCO2.Add("name", "CO2");
                tagCO2.Add("value", measureCO2);
                tags.Add(tagCO2);

                tagNH3.Add("name", "NH3");
                tagNH3.Add("value", measureNH3);
                tags.Add(tagNH3);

                tagHUMID.Add("name", "습도");
                tagHUMID.Add("value", measureHUMID);
                tags.Add(tagHUMID);

                tagTEMP.Add("name", "온도");
                tagTEMP.Add("value", measureTEMP.ToString());
                tags.Add(tagTEMP);

                bodyMessage.Add("tags", tags);

                var client = new HttpClient();
                client.DefaultRequestHeaders.ExpectContinue = false; // <-- Make sure it is false.
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //var httpContent = new StringContent(bodyMessage.ToString(), System.Text.Encoding.UTF8, "application/json");
                var httpContent = new StringContent(bodyMessage.ToString(Formatting.None), System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(requestUrl, httpContent);

                // 응답 데이터 가져오기
                var responseJson = await response.Content.ReadAsStringAsync();

                // 응답 데이터 역직렬화
                var responseData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);

                JObject parseJSON = JObject.Parse(responseJson);

                var id = (int)parseJSON["diaryId"];

                //이미지 파일 업로드
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
                            .Replace("{id}", id.ToString());

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
