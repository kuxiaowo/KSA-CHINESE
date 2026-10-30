using HarmonyLib;
using KSA;

namespace KsaChineseSmoke;

public sealed partial class Driver
{
    void Update5707Frame(double t)
    {
        if(Stage==0 && t>3)
        {
            ContentSnapshot(); GameSettings.Show=false;
            SpaceWeatherWindow.Toggle();
            var window=AccessTools.Field(typeof(SpaceWeatherWindow),"_instance").GetValue(null)!;
            AccessTools.Field(window.GetType(),"_pickedStarId").SetValue(window,"Sol");
            AccessTools.Field(window.GetType(),"_pickedBodyId").SetValue(window,"Earth");
            Stage++;
        }
        else if(Stage==1 && t>8) { Shot("5707-weather-zh");Stage++; }
        else if(Stage==2 && t>12) { Locale("en-US");Stage++; }
        else if(Stage==3 && t>16) { Shot("5707-weather-en");Stage++; }
        else if(Stage==4 && t>20) { Locale("zh-CN");SpaceWeatherWindow.Toggle();KittenRosterWindow.Toggle();Stage++; }
        else if(Stage==5 && t>25) { Shot("5707-roster-zh");Stage++; }
        else if(Stage==6 && t>29) { KittenRosterWindow.Toggle();Tab("Display");Stage++; }
        else if(Stage==7 && t>34) { Shot("5707-display-zh");Stage++; }
        else if(Stage==8 && t>38) { GameSettings.Show=false;History(true);Stage++; }
        else if(Stage==9 && t>43) { Shot("5707-history-zh");Stage++; }
        else if(Stage==10 && t>47)
        {
            History(false);Program.MainViewport.SetCameraMode(CameraMode.Map);
            foreach(var canvas in GaugeCanvas.AllCanvases)canvas.SetEnabled(canvas.Id=="Radiation");
            Stage++;
        }
        else if(Stage==11 && t>52) { Shot("5707-map-radiation-zh");Stage++; }
        else if(Stage==12 && t>56) { GameSaves.MakeUncompressedSave("ksa_chinese_5707_test");Log("5707-save-request");Stage++; }
        else if(Stage==13 && t>63) { GameSaves.LoadSaveGame("ksa_chinese_5707_test");Log("5707-load-request");Stage++; }
        else if(Stage==14 && t>74) { ContentSnapshot();Log("5707-review-completed");Program.Instance.RequestExit(0);Stage++; }
    }
}
