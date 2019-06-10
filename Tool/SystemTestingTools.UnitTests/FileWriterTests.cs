using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace SystemTestingTools.UnitTests
{
    [Trait("Project", "SystemTestingTools Unit Tests (unhappy)")]
    public class FileWriterTests
    {
        readonly string folder = Path.Combine(Path.GetTempPath(),"SystemTestingTools");
        readonly string expectedFileContent = @"METADATA
Date: 2019-03-17 15:44:37.597 my_time_zone
Requested by code: C:\temp\code.cs
Local machine: Machine001
User: A01/MyUser
Using tool: SystemTestingTools 0.1.0.0 (http://www.whatever.com)
Observations:

REQUEST
post https://www.whatever.com/someendpoint
User-Agent:MyApp
{""user"":""Alan"", ""trickyField"":""--!?@Divider:"", ""trickyField2"":""HTTP/1.1 200 OK""}

--!?@Divider: Any text BEFORE this line = comments, AFTER = response in Fiddler like format

HTTP/1.1 200 OK
Server:Kestrel

{""value"":""whatever"", ""trickyField"":""--!?@Divider:"", ""trickyField2"":""HTTP/1.1 200 OK""}";

        [Fact]
        public async Task When_FilesAlreadyExistInFolder_And_ValidRequestResponse_Then_CreateTextFileInRightFormat_And_CanLoadFile()
        {
            // this test relies on usage of the FileSystem, it's unusual, but considered best balance 
            // of efficient + realist testing vs potential downsides, using the temporary folder of the machine
            // should be ok

            // arrange
            if(Directory.Exists(folder)) Directory.Delete(folder, true); // if folder exists, it was the result of previous tests

            // create directory and some files in it, so we make sure our code creates the next file correctly
            Directory.CreateDirectory(folder);
            File.CreateText(Path.Combine(folder, "1_OK.txt"));
            File.CreateText(Path.Combine(folder, "2_Forbidden.txt"));
            File.CreateText(Path.Combine(folder, "28_Whatever.txt"));
            File.CreateText(Path.Combine(folder, "some_random_file.txt"));

            var input = new RequestResponse()
            {
                Metadata = new RequestResponse.MetadataInfo()
                {
                    DateTime = System.DateTime.Parse("2019-03-17 15:44:37.597"),
                    Timezone = "my_time_zone",
                    LocalMachine = "Machine001",
                    User = "A01/MyUser",
                    RequestedByCode = @"C:\temp\code.cs",
                    ToolUrl = "http://www.whatever.com",
                    ToolNameAndVersion = "SystemTestingTools 0.1.0.0"
                },
                Request = new RequestResponse.RequestInfo()
                {
                    Method = HttpMethod.Post,
                    Url = "https://www.whatever.com/someendpoint",
                    Body = @"{""user"":""Alan"", ""trickyField"":""--!?@Divider:"", ""trickyField2"":""HTTP/1.1 200 OK""}",
                    Headers = new Dictionary<string, string>() { { "User-Agent", "MyApp" } }
                },
                Response = new RequestResponse.ResponseInfo()
                {
                    Status = HttpStatusCode.OK,
                    Body = @"{""value"":""whatever"", ""trickyField"":""--!?@Divider:"", ""trickyField2"":""HTTP/1.1 200 OK""}",
                    HttpVersion = new System.Version(1,1),
                    Headers = new Dictionary<string, string>() { { "Server", "Kestrel" } }
                }
            };

            var sut = new FileWriter(folder);

            // act
            var fileName = sut.Write(input);

            // asserts
            fileName.ShouldBe("29_OK");
            var createdFile = Path.Combine(folder, "29_OK.txt");
            File.Exists(createdFile).ShouldBeTrue();

            var content = File.ReadAllText(createdFile);

            content.ShouldBe(expectedFileContent);

            var deserializedResponse = ResponseFactory.FromFiddlerLikeResponseFile(createdFile);

            deserializedResponse.StatusCode.ShouldBe(input.Response.Status);
            (await deserializedResponse.Content.ReadAsStringAsync()).ShouldBe(input.Response.Body);

            foreach (var item in input.Response.Headers)
                deserializedResponse.Headers.ShouldContainHeader(item.Key, item.Value);
        }

        [Fact]
        public void When_ContentTypeIsJson_FormatBody()
        {
            var sut = new FileWriter(folder);

            var json = @"{""glossary"": {""title"": ""example glossary"",""GlossDiv"": {""title"": ""S"",""GlossList"": {""GlossEntry"": {""ID"": ""SGML"",""SortAs"": ""SGML"",""GlossTerm"": ""Standard Generalized Markup Language"",""Acronym"": ""SGML"",""Abbrev"": ""ISO 8879:1986"",""GlossDef"": {""para"": ""A meta-markup language, used to create markup languages such as DocBook."",""GlossSeeAlso"": [""GML"", ""XML""]},""GlossSee"": ""markup""}}}}}";

            // act
            var body = sut.FormatBody(json, @"Application/Json");

            // asserts
            body.ShouldBe(@"{
    ""glossary"":  {
        ""title"":  ""example glossary"",
        ""GlossDiv"":  {
            ""title"":  ""S"",
            ""GlossList"":  {
                ""GlossEntry"":  {
                    ""ID"":  ""SGML"",
                    ""SortAs"":  ""SGML"",
                    ""GlossTerm"":  ""Standard Generalized Markup Language"",
                    ""Acronym"":  ""SGML"",
                    ""Abbrev"":  ""ISO 8879:1986"",
                    ""GlossDef"":  {
                        ""para"":  ""A meta-markup language, used to create markup languages such as DocBook."",
                        ""GlossSeeAlso"":  [
                            ""GML"",
                             ""XML""
                        ]
                    },
                    ""GlossSee"":  ""markup""
                }
            }
        }
    }
}");
        }
    }
}