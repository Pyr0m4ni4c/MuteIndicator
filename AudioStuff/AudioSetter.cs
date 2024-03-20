namespace AudioStuff
{
    public static class AudioSetter
    {
        public static void SetDefault(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            CoreAudioApi.PolicyConfigClient client = new CoreAudioApi.PolicyConfigClient();
            client.SetDefaultEndpoint(id, CoreAudioApi.ERole.eCommunications);
            client.SetDefaultEndpoint(id, CoreAudioApi.ERole.eMultimedia);
        }
    }
}