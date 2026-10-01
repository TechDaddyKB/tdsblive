using System;

public class CPHInline
{
    public bool Execute()
    {
        string value;
        Guid testId;
        if (!CPH.TryGetArg<string>("tdsbliveTestId", out value) && !CPH.TryGetArg<string>("tdsbliveEventId", out value)) return false;
        if (!Guid.TryParse(value, out testId)) return false;
        // This action only changes a namespaced test marker and emits a synthetic event.
        // It never sends chat, controls OBS, speaks, or contributes to financial totals.
        CPH.SetGlobalVar("tdsbliveG03LastTestId", testId.ToString(), true);
        CPH.WebsocketBroadcastJson("{\"tdsbliveForwardedSource\":\"tdsblive-test\",\"tdsbliveForwardedType\":\"Probe\",\"tdsbliveProvenance\":\"simulation\",\"tdsbliveTest\":true,\"payload\":{\"messageId\":\"" + testId.ToString() + "\",\"message\":\"TDSBLive G03 trigger qualification\"}}");
        CPH.LogInfo("TDSBLive G03 qualification action executed.");
        return true;
    }
}
