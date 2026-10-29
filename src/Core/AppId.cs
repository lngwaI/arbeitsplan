namespace Arbeitsplan.Core
{
    // Ordnername fuer Benutzereinstellungen unter %APPDATA% (Demo-Build getrennt von der echten Anwendung, siehe docs/Demo.md)
    public static class AppId
    {
#if DEMO
        public static readonly bool IsDemo = true;
        public const string Dir = "ArbeitsplanDemo";
#else
        public static readonly bool IsDemo = false;
        public const string Dir = "Arbeitsplan";
#endif
    }
}
