using System.Text.Json.Serialization;

namespace GraduationProject.Models
{
    public class FirebaseSettings
    {
        [JsonPropertyName("project_id")]
        public string ProjectId => "evaluation-project-eed94";

        [JsonPropertyName("private_key_id")]
        public string PrivateKeyId => "0WxWudPqL1aV6dO7Me4yWOnvMJXC4ilNbnrpjCyg";

    }
}
