////----------------------------------------------------------------------------////
////
////                        Reiknistofa Bankanna - Auðkenning
////                                CallbacksDto.cs
////
////----------------------------------------------------------------------------////

using Newtonsoft.Json;
using System.Text;

namespace Audkenning.Dtos
{
    /// <summary>
    /// The callback dto that is sent from the server
    /// </summary>
    public class CallbacksDto
    {
        [JsonProperty("authId")]
        public string AuthId { get; set; }

        [JsonProperty("callbacks")]
        public List<Callback> Callbacks { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"AuthId: {AuthId}");
            sb.AppendLine("Callbacks:");
            foreach (var callback in Callbacks)
            {
                sb.AppendLine(callback.ToString());  // Add line breaks for each callback
            }
            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// The shorter version of callbacks - Will rename later
    /// </summary>
    public class GetCallbackDto2
    {
        [JsonProperty("authId")]
        public string AuthId { get; set; }

        [JsonProperty("callbacks")]
        public List<Callback2> Callbacks { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"AuthId: {AuthId}");
            sb.AppendLine("Callbacks:");
            foreach (var callback in Callbacks)
            {
                sb.AppendLine(callback.ToString());  // Add line breaks for each callback
            }
            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// Contains the shorter version of callbacks - Will rename later
    /// </summary>
    public class Callback2
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("output")]
        public List<Output> Output { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\tCallback Type: {Type}");

            sb.AppendLine("\tOutputs:");
            foreach (var o in Output)
            {
                sb.Append($"\t{o.ToString()}");
            }
            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// Contains the callbacks
    /// </summary>
    public class Callback
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("output")]
        public List<Output> Output { get; set; }

        [JsonProperty("input")]
        public List<Input> Input { get; set; }

        [JsonProperty("_id")]
        public int? Id { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\tCallback Type: {Type}");
            sb.AppendLine($"\tCallback Id: {Id}");

            sb.AppendLine("\tOutputs:");
            foreach (var o in Output)
            {
                sb.Append($"\t{o.ToString()}");
            }

            sb.AppendLine("\tInputs:");
            if (Input != null)
            {
                foreach (var i in Input)
                {
                    sb.Append($"\t{i.ToString()}");
                }
            }
            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// Contains the name and value of an output
    /// </summary>
    public class Output
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        public override string ToString()
        {
            return $"\t\t{Name}: {Value}\n";
        }
    }

    /// <summary>
    /// Contains the name and value of an input
    /// </summary>
    public class Input
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        public override string ToString()
        {
            return $"\t\t{Name}: {Value}\n";
        }
    }
}
