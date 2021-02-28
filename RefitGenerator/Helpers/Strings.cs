namespace RefitGenerator.Helpers
{
    static class Strings
    {
        public const string Indent = "    ";
        public const string Indent2 = Indent + Indent;
        public const string Indent3 = Indent2 + Indent;
        public const string JpnFormat = "[JsonPropertyName(\"{0}\")]";
        public const string RefitAttributeFormat = "[{0}(\"{1}\")]";
        public const string ModelPropFormat = "public {0} {1} {{ get; set; }}";
        public const string ApiPropFormat = "public I{0}Api {0}Api {{ get; }}";
        public const string MultipartFormData = "multipart/form-data";
        public const string FormDataUrlEncoded = "application/x-www-form-urlencoded";
        public const string TemplatesDirectory = "Templates";
        public const string ModelsDirectory = "Models";
        public const string ApisDirectory = "Apis";
    }
}
