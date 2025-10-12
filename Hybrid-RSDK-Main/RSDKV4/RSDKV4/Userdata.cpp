#include "RetroEngine.hpp"

int globalVariablesCount;
int globalVariables[0x1000];  // Use larger size to match Script.hpp
char globalVariableNames[0x1000][0x20];  // Use larger size to match Script.hpp

void *nativeFunction[NATIIVEFUNCTION_COUNT];
int nativeFunctionCount = 0;

char gamePath[0x100];
int saveRAM[SAVEDATA_SIZE];
Achievement achievements[ACHIEVEMENT_COUNT];
int achievementCount = 0;

LeaderboardEntry leaderboards[LEADERBOARD_COUNT];

MultiplayerData multiplayerDataIN  = MultiplayerData();
MultiplayerData multiplayerDataOUT = MultiplayerData();
int matchValueData[0x100];
byte matchValueReadPos  = 0;
byte matchValueWritePos = 0;

int vsGameLength = 4;
int vsItemMode   = 1;
int vsPlayerID   = 0;
bool vsPlaying   = false;

int sendCounter = 0;

#if RETRO_PLATFORM == RETRO_OSX || RETRO_PLATFORM == RETRO_LINUX
#include <sys/stat.h>
#include <sys/types.h>
#endif

#if !RETRO_USE_ORIGINAL_CODE
bool forceUseScripts         = false;
bool forceUseScripts_Config  = false;
bool skipStartMenu           = false;
bool skipStartMenu_Config    = false;
int disableFocusPause        = 0;
int disableFocusPause_Config = 0;

bool useSGame = false;

bool ReadSaveRAMData()
{
    useSGame = false;
    char buffer[0x180];
#if RETRO_USE_MOD_LOADER
#if RETRO_PLATFORM == RETRO_UWP
    if (!usingCWD)
        sprintf(buffer, "%s/%sSData.bin", redirectSave ? modsPath : getResourcesPath(), savePath);
    else
        sprintf(buffer, "%s%sSData.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
    sprintf(buffer, "%s/%sSData.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
    sprintf(buffer, "%s/%sSData.bin", redirectSave ? modsPath : getDocumentsPath(), savePath);
#else
    sprintf(buffer, "%s%sSData.bin", redirectSave ? modsPath : gamePath, savePath);
#endif
#else
#if RETRO_PLATFORM == RETRO_UWP
    if (!usingCWD)
        sprintf(buffer, "%s/%sSData.bin", getResourcesPath(), savePath);
    else
        sprintf(buffer, "%s%sSData.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
    sprintf(buffer, "%s/%sSData.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
    sprintf(buffer, "%s/%sSData.bin", getDocumentsPath(), savePath);
#else
    sprintf(buffer, "%s%sSData.bin", gamePath, savePath);
#endif
#endif

    FileIO *saveFile = fOpen(buffer, "rb");
    if (!saveFile) {
#if RETRO_USE_MOD_LOADER
#if RETRO_PLATFORM == RETRO_UWP
        if (!usingCWD)
            sprintf(buffer, "%s/%sSGame.bin", redirectSave ? modsPath : getResourcesPath(), savePath);
        else
            sprintf(buffer, "%s%sSGame.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
        sprintf(buffer, "%s/%sSGame.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
        sprintf(buffer, "%s/%sSGame.bin", redirectSave ? modsPath : getDocumentsPath(), savePath);
#else
        sprintf(buffer, "%s%sSGame.bin", redirectSave ? modsPath : gamePath, savePath);
#endif
#else
#if RETRO_PLATFORM == RETRO_UWP
        if (!usingCWD)
            sprintf(buffer, "%s/%sSGame.bin", getResourcesPath(), savePath);
        else
            sprintf(buffer, "%s%sSGame.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
        sprintf(buffer, "%s/%sSGame.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
        sprintf(buffer, "%s/%sSGame.bin", getDocumentsPath(), savePath);
#else
        sprintf(buffer, "%s%sSGame.bin", gamePath, savePath);
#endif
#endif

        saveFile = fOpen(buffer, "rb");
        if (!saveFile)
            return false;
        useSGame = true;
    }
    fRead(saveRAM, sizeof(int), SAVEDATA_SIZE, saveFile);
    fClose(saveFile);
    return true;
}

bool WriteSaveRAMData()
{
    char buffer[0x180];

    if (!useSGame) {
#if RETRO_USE_MOD_LOADER
#if RETRO_PLATFORM == RETRO_UWP
        if (!usingCWD)
            sprintf(buffer, "%s/%sSData.bin", redirectSave ? modsPath : getResourcesPath(), savePath);
        else
            sprintf(buffer, "%s%sSData.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
        sprintf(buffer, "%s/%sSData.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
        sprintf(buffer, "%s/%sSData.bin", redirectSave ? modsPath : getDocumentsPath(), savePath);
#else
        sprintf(buffer, "%s%sSData.bin", redirectSave ? modsPath : gamePath, savePath);
#endif
#else
#if RETRO_PLATFORM == RETRO_UWP
        if (!usingCWD)
            sprintf(buffer, "%s/%sSData.bin", getResourcesPath(), savePath);
        else
            sprintf(buffer, "%s%sSData.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
        sprintf(buffer, "%s/%sSData.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
        sprintf(buffer, "%s/%sSData.bin", getDocumentsPath(), savePath);
#else
        sprintf(buffer, "%s%sSData.bin", gamePath, savePath);
#endif
#endif
    }
    else {
#if RETRO_USE_MOD_LOADER
#if RETRO_PLATFORM == RETRO_UWP
        if (!usingCWD)
            sprintf(buffer, "%s/%sSGame.bin", redirectSave ? modsPath : getResourcesPath(), savePath);
        else
            sprintf(buffer, "%s%sSGame.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
        sprintf(buffer, "%s/%sSGame.bin", redirectSave ? modsPath : gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
        sprintf(buffer, "%s/%sSGame.bin", redirectSave ? modsPath : getDocumentsPath(), savePath);
#else
        sprintf(buffer, "%s%sSGame.bin", redirectSave ? modsPath : gamePath, savePath);
#endif
#else
#if RETRO_PLATFORM == RETRO_UWP
        if (!usingCWD)
            sprintf(buffer, "%s/%sSGame.bin", getResourcesPath(), savePath);
        else
            sprintf(buffer, "%s%sSGame.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_OSX
        sprintf(buffer, "%s/%sSGame.bin", gamePath, savePath);
#elif RETRO_PLATFORM == RETRO_iOS
        sprintf(buffer, "%s/%sSGame.bin", getDocumentsPath(), savePath);
#else
        sprintf(buffer, "%s%sSGame.bin", gamePath, savePath);
#endif
#endif
    }

    FileIO *saveFile = fOpen(buffer, "wb");
    if (!saveFile)
        return false;
    fWrite(saveRAM, sizeof(int), SAVEDATA_SIZE, saveFile);
    fClose(saveFile);
    return true;
}

void InitUserdata()
{
    // userdata files are loaded from this directory
    sprintf(gamePath, "%s", BASE_PATH);
#if RETRO_USE_MOD_LOADER
    sprintf(modsPath, "%s", BASE_PATH);
#endif

#if RETRO_PLATFORM == RETRO_OSX
    sprintf(gamePath, "%s/RSDKv4", getResourcesPath());
    sprintf(modsPath, "%s/RSDKv4/", getResourcesPath());

    mkdir(gamePath, 0777);
#elif RETRO_PLATFORM == RETRO_ANDROID
    {
        char buffer[0x200];

        JNIEnv *env      = (JNIEnv *)SDL_AndroidGetJNIEnv();
        jobject activity = (jobject)SDL_AndroidGetActivity();
        jclass cls(env->GetObjectClass(activity));
        jmethodID method = env->GetMethodID(cls, "getBasePath", "()Ljava/lang/String;");
        auto ret         = env->CallObjectMethod(activity, method);

        strcpy(buffer, env->GetStringUTFChars((jstring)ret, NULL));

        sprintf(gamePath, "%s", buffer);
#if RETRO_USE_MOD_LOADER
        sprintf(modsPath, "%s", buffer);
#endif

        env->DeleteLocalRef(activity);
        env->DeleteLocalRef(cls);
    }
#endif

    char buffer[0x100];
#if RETRO_PLATFORM == RETRO_UWP
    if (!usingCWD)
        sprintf(buffer, "%s/settings.ini", getResourcesPath());
    else
        sprintf(buffer, "%ssettings.ini", gamePath);
#elif RETRO_PLATFORM == RETRO_OSX || RETRO_PLATFORM == RETRO_ANDROID
    sprintf(buffer, "%s/settings.ini", gamePath);
#else
    sprintf(buffer, BASE_PATH "settings.ini");
#endif
    FileIO *file = fOpen(buffer, "rb");
    if (!file) {
        IniParser ini;

        ini.SetBool("Dev", "DevMenu", Engine.devMenu = false);
        ini.SetBool("Dev", "EngineDebugMode", engineDebugMode = false);
        ini.SetBool("Dev", "TxtScripts", forceUseScripts = false);
        forceUseScripts_Config = forceUseScripts;
        ini.SetInteger("Dev", "StartingCategory", Engine.startList = 255);
        ini.SetInteger("Dev", "StartingScene", Engine.startStage = 255);
        ini.SetInteger("Dev", "StartingPlayer", Engine.startPlayer = 255);
        ini.SetInteger("Dev", "StartingSaveFile", Engine.startSave = 255);
        ini.SetInteger("Dev", "FastForwardSpeed", Engine.fastForwardSpeed = 8);
        Engine.startList_Game  = Engine.startList;
        Engine.startStage_Game = Engine.startStage;

        ini.SetBool("Dev", "UseHQModes", Engine.useHQModes = true);
        ini.SetString("Dev", "DataFile", (char *)"Data.rsdk");
        StrCopy(Engine.dataFile[0], "Data.rsdk");
        if (!StrComp(Engine.dataFile[1], "")) {
            ini.SetString("Dev", "DataFile2", (char *)"Data2.rsdk");
            StrCopy(Engine.dataFile[1], "Data2.rsdk");
        }
        if (!StrComp(Engine.dataFile[2], "")) {
            ini.SetString("Dev", "DataFile3", (char *)"Data3.rsdk");
            StrCopy(Engine.dataFile[2], "Data3.rsdk");
        }
        if (!StrComp(Engine.dataFile[3], "")) {
            ini.SetString("Dev", "DataFile4", (char *)"Data4.rsdk");
            StrCopy(Engine.dataFile[3], "Data4.rsdk");
        }

        ini.SetInteger("Game", "Language", Engine.language = RETRO_EN);
        ini.SetInteger("Game", "GameType", Engine.gameTypeID = 0);
        ini.SetBool("Game", "SkipStartMenu", skipStartMenu = false);
        skipStartMenu_Config = skipStartMenu;
        ini.SetInteger("Game", "DisableFocusPause", disableFocusPause = 0);
        disableFocusPause_Config = disableFocusPause;

#if RETRO_USE_NETWORKING
        ini.SetString("Network", "Host", (char *)"127.0.0.1");
        StrCopy(networkHost, "127.0.0.1");
        ini.SetInteger("Network", "Port", networkPort = 50);
#endif

        ini.SetBool("Window", "FullScreen", Engine.startFullScreen = DEFAULT_FULLSCREEN);
        ini.SetBool("Window", "Borderless", Engine.borderless = false);
        ini.SetBool("Window", "VSync", Engine.vsync = false);
        ini.SetInteger("Window", "ScalingMode", Engine.scalingMode = 0);
        ini.SetInteger("Window", "WindowScale", Engine.windowScale = 2);
        ini.SetInteger("Window", "ScreenWidth", SCREEN_XSIZE_CONFIG = DEFAULT_SCREEN_XSIZE);
        SCREEN_XSIZE = SCREEN_XSIZE_CONFIG;
        ini.SetInteger("Window", "RefreshRate", Engine.refreshRate = 60);
        ini.SetInteger("Window", "DimLimit", Engine.dimLimit = 300);
        Engine.dimLimit *= Engine.refreshRate;

        ini.SetFloat("Audio", "BGMVolume", bgmVolume / (float)MAX_VOLUME);
        ini.SetFloat("Audio", "SFXVolume", sfxVolume / (float)MAX_VOLUME);

#if RETRO_USING_SDL2
        ini.SetInteger("Keyboard 1", "Up", inputDevice[INPUT_UP].keyMappings = SDL_SCANCODE_UP);
        ini.SetInteger("Keyboard 1", "Down", inputDevice[INPUT_DOWN].keyMappings = SDL_SCANCODE_DOWN);
        ini.SetInteger("Keyboard 1", "Left", inputDevice[INPUT_LEFT].keyMappings = SDL_SCANCODE_LEFT);
        ini.SetInteger("Keyboard 1", "Right", inputDevice[INPUT_RIGHT].keyMappings = SDL_SCANCODE_RIGHT);
        ini.SetInteger("Keyboard 1", "A", inputDevice[INPUT_BUTTONA].keyMappings = SDL_SCANCODE_Z);
        ini.SetInteger("Keyboard 1", "B", inputDevice[INPUT_BUTTONB].keyMappings = SDL_SCANCODE_X);
        ini.SetInteger("Keyboard 1", "C", inputDevice[INPUT_BUTTONC].keyMappings = SDL_SCANCODE_C);
        ini.SetInteger("Keyboard 1", "X", inputDevice[INPUT_BUTTONX].keyMappings = SDL_SCANCODE_A);
        ini.SetInteger("Keyboard 1", "Y", inputDevice[INPUT_BUTTONY].keyMappings = SDL_SCANCODE_S);
        ini.SetInteger("Keyboard 1", "Z", inputDevice[INPUT_BUTTONZ].keyMappings = SDL_SCANCODE_D);
        ini.SetInteger("Keyboard 1", "L", inputDevice[INPUT_BUTTONL].keyMappings = SDL_SCANCODE_Q);
        ini.SetInteger("Keyboard 1", "R", inputDevice[INPUT_BUTTONR].keyMappings = SDL_SCANCODE_E);
        ini.SetInteger("Keyboard 1", "Start", inputDevice[INPUT_START].keyMappings = SDL_SCANCODE_RETURN);
        ini.SetInteger("Keyboard 1", "Select", inputDevice[INPUT_SELECT].keyMappings = SDL_SCANCODE_TAB);

        ini.SetInteger("Controller 1", "Up", inputDevice[INPUT_UP].contMappings = SDL_CONTROLLER_BUTTON_DPAD_UP);
        ini.SetInteger("Controller 1", "Down", inputDevice[INPUT_DOWN].contMappings = SDL_CONTROLLER_BUTTON_DPAD_DOWN);
        ini.SetInteger("Controller 1", "Left", inputDevice[INPUT_LEFT].contMappings = SDL_CONTROLLER_BUTTON_DPAD_LEFT);
        ini.SetInteger("Controller 1", "Right", inputDevice[INPUT_RIGHT].contMappings = SDL_CONTROLLER_BUTTON_DPAD_RIGHT);
        ini.SetInteger("Controller 1", "A", inputDevice[INPUT_BUTTONA].contMappings = SDL_CONTROLLER_BUTTON_A);
        ini.SetInteger("Controller 1", "B", inputDevice[INPUT_BUTTONB].contMappings = SDL_CONTROLLER_BUTTON_B);
        ini.SetInteger("Controller 1", "C", inputDevice[INPUT_BUTTONC].contMappings = SDL_CONTROLLER_BUTTON_X);
        ini.SetInteger("Controller 1", "X", inputDevice[INPUT_BUTTONX].contMappings = SDL_CONTROLLER_BUTTON_Y);
        ini.SetInteger("Controller 1", "Y", inputDevice[INPUT_BUTTONY].contMappings = SDL_CONTROLLER_BUTTON_ZL);
        ini.SetInteger("Controller 1", "Z", inputDevice[INPUT_BUTTONZ].contMappings = SDL_CONTROLLER_BUTTON_ZR);
        ini.SetInteger("Controller 1", "L", inputDevice[INPUT_BUTTONL].contMappings = SDL_CONTROLLER_BUTTON_LEFTSHOULDER);
        ini.SetInteger("Controller 1", "R", inputDevice[INPUT_BUTTONR].contMappings = SDL_CONTROLLER_BUTTON_RIGHTSHOULDER);
        ini.SetInteger("Controller 1", "Start", inputDevice[INPUT_START].contMappings = SDL_CONTROLLER_BUTTON_START);
        ini.SetInteger("Controller 1", "Select", inputDevice[INPUT_SELECT].contMappings = SDL_CONTROLLER_BUTTON_GUIDE);

        ini.SetFloat("Controller 1", "LStickDeadzone", LSTICK_DEADZONE = 0.3);
        ini.SetFloat("Controller 1", "RStickDeadzone", RSTICK_DEADZONE = 0.3);
        ini.SetFloat("Controller 1", "LTriggerDeadzone", LTRIGGER_DEADZONE = 0.3);
        ini.SetFloat("Controller 1", "RTriggerDeadzone", RTRIGGER_DEADZONE = 0.3);
#endif

#if RETRO_USING_SDL1
        ini.SetInteger("Keyboard 1", "Up", inputDevice[INPUT_UP].keyMappings = SDLK_UP);
        ini.SetInteger("Keyboard 1", "Down", inputDevice[INPUT_DOWN].keyMappings = SDLK_DOWN);
        ini.SetInteger("Keyboard 1", "Left", inputDevice[INPUT_LEFT].keyMappings = SDLK_LEFT);
        ini.SetInteger("Keyboard 1", "Right", inputDevice[INPUT_RIGHT].keyMappings = SDLK_RIGHT);
        ini.SetInteger("Keyboard 1", "A", inputDevice[INPUT_BUTTONA].keyMappings = SDLK_z);
        ini.SetInteger("Keyboard 1", "B", inputDevice[INPUT_BUTTONB].keyMappings = SDLK_x);
        ini.SetInteger("Keyboard 1", "C", inputDevice[INPUT_BUTTONC].keyMappings = SDLK_c);
        ini.SetInteger("Keyboard 1", "X", inputDevice[INPUT_BUTTONX].keyMappings = SDLK_a);
        ini.SetInteger("Keyboard 1", "Y", inputDevice[INPUT_BUTTONY].keyMappings = SDLK_s);
        ini.SetInteger("Keyboard 1", "Z", inputDevice[INPUT_BUTTONZ].keyMappings = SDLK_d);
        ini.SetInteger("Keyboard 1", "L", inputDevice[INPUT_BUTTONL].keyMappings = SDLK_q);
        ini.SetInteger("Keyboard 1", "R", inputDevice[INPUT_BUTTONR].keyMappings = SDLK_e);
        ini.SetInteger("Keyboard 1", "Start", inputDevice[INPUT_START].keyMappings = SDLK_RETURN);
        ini.SetInteger("Keyboard 1", "Select", inputDevice[INPUT_SELECT].keyMappings = SDLK_TAB);

        ini.SetInteger("Controller 1", "Up", inputDevice[INPUT_UP].contMappings = 1);
        ini.SetInteger("Controller 1", "Down", inputDevice[INPUT_DOWN].contMappings = 2);
        ini.SetInteger("Controller 1", "Left", inputDevice[INPUT_LEFT].contMappings = 3);
        ini.SetInteger("Controller 1", "Right", inputDevice[INPUT_RIGHT].contMappings = 4);
        ini.SetInteger("Controller 1", "A", inputDevice[INPUT_BUTTONA].contMappings = 5);
        ini.SetInteger("Controller 1", "B", inputDevice[INPUT_BUTTONB].contMappings = 6);
        ini.SetInteger("Controller 1", "C", inputDevice[INPUT_BUTTONC].contMappings = 7);
        ini.SetInteger("Controller 1", "X", inputDevice[INPUT_BUTTONX].contMappings = 9);
        ini.SetInteger("Controller 1", "Y", inputDevice[INPUT_BUTTONY].contMappings = 10);
        ini.SetInteger("Controller 1", "Z", inputDevice[INPUT_BUTTONZ].contMappings = 11);
        ini.SetInteger("Controller 1", "L", inputDevice[INPUT_BUTTONL].contMappings = 12);
        ini.SetInteger("Controller 1", "R", inputDevice[INPUT_BUTTONR].contMappings = 13);
        ini.SetInteger("Controller 1", "Start", inputDevice[INPUT_START].contMappings = 8);
        ini.SetInteger("Controller 1", "Select", inputDevice[INPUT_SELECT].contMappings = 14);

        ini.SetFloat("Controller 1", "LStickDeadzone", LSTICK_DEADZONE = 0.3);
        ini.SetFloat("Controller 1", "RStickDeadzone", RSTICK_DEADZONE = 0.3);
        ini.SetFloat("Controller 1", "LTriggerDeadzone", LTRIGGER_DEADZONE = 0.3);
        ini.SetFloat("Controller 1", "RTriggerDeadzone", RTRIGGER_DEADZONE = 0.3);
#endif

        ini.Write(buffer);
    }
    else {
        fClose(file);
        IniParser ini(buffer, false);

        if (!ini.GetBool("Dev", "DevMenu", &Engine.devMenu))
            Engine.devMenu = false;
        if (!ini.GetBool("Dev", "EngineDebugMode", &engineDebugMode))
            engineDebugMode = false;
        if (!ini.GetBool("Dev", "TxtScripts", &forceUseScripts))
            forceUseScripts = false;
        forceUseScripts_Config = forceUseScripts;
        if (!ini.GetInteger("Dev", "StartingCategory", &Engine.startList))
            Engine.startList = 255;
        if (!ini.GetInteger("Dev", "StartingScene", &Engine.startStage))
            Engine.startStage = 255;
        if (!ini.GetInteger("Dev", "StartingPlayer", &Engine.startPlayer))
            Engine.startPlayer = 255;
        if (!ini.GetInteger("Dev", "StartingSaveFile", &Engine.startSave))
            Engine.startSave = 255;
        if (!ini.GetInteger("Dev", "FastForwardSpeed", &Engine.fastForwardSpeed))
            Engine.fastForwardSpeed = 8;
        if (!ini.GetBool("Dev", "UseHQModes", &Engine.useHQModes))
            Engine.useHQModes = true;

        Engine.startList_Game  = Engine.startList;
        Engine.startStage_Game = Engine.startStage;

        if (!ini.GetString("Dev", "DataFile", Engine.dataFile[0]))
            StrCopy(Engine.dataFile[0], "Data.rsdk");
        if (!StrComp(Engine.dataFile[1], "")) {
            if (!ini.GetString("Dev", "DataFile2", Engine.dataFile[1]))
                StrCopy(Engine.dataFile[1], "");
        }
        if (!StrComp(Engine.dataFile[2], "")) {
            if (!ini.GetString("Dev", "DataFile3", Engine.dataFile[2]))
                StrCopy(Engine.dataFile[2], "");
        }
        if (!StrComp(Engine.dataFile[3], "")) {
            if (!ini.GetString("Dev", "DataFile4", Engine.dataFile[3]))
                StrCopy(Engine.dataFile[3], "");
        }

        if (!ini.GetInteger("Game", "Language", &Engine.language))
            Engine.language = RETRO_EN;
        if (!ini.GetInteger("Game", "GameType", &Engine.gameTypeID))
            Engine.gameTypeID = 0;
        Engine.releaseType = Engine.gameTypeID ? "USE_ORIGINS" : "USE_STANDALONE";

        if (!ini.GetBool("Game", "SkipStartMenu", &skipStartMenu))
            skipStartMenu = false;
        skipStartMenu_Config = skipStartMenu;
        if (!ini.GetInteger("Game", "DisableFocusPause", &disableFocusPause))
            disableFocusPause = false;
        disableFocusPause_Config = disableFocusPause;

#if RETRO_USE_NETWORKING
        if (!ini.GetString("Network", "Host", networkHost))
            StrCopy(networkHost, "127.0.0.1");
        if (!ini.GetInteger("Network", "Port", &networkPort))
            networkPort = 50;
#endif

        if (!ini.GetBool("Window", "FullScreen", &Engine.startFullScreen))
            Engine.startFullScreen = DEFAULT_FULLSCREEN;
        if (!ini.GetBool("Window", "Borderless", &Engine.borderless))
            Engine.borderless = false;
        if (!ini.GetBool("Window", "VSync", &Engine.vsync))
            Engine.vsync = false;
        if (!ini.GetInteger("Window", "ScalingMode", &Engine.scalingMode))
            Engine.scalingMode = 0;
        if (!ini.GetInteger("Window", "WindowScale", &Engine.windowScale))
            Engine.windowScale = 2;
        if (!ini.GetInteger("Window", "ScreenWidth", &SCREEN_XSIZE_CONFIG))
            SCREEN_XSIZE_CONFIG = DEFAULT_SCREEN_XSIZE;
        SCREEN_XSIZE = SCREEN_XSIZE_CONFIG;
        if (!ini.GetInteger("Window", "RefreshRate", &Engine.refreshRate))
            Engine.refreshRate = 60;
        if (!ini.GetInteger("Window", "DimLimit", &Engine.dimLimit))
            Engine.dimLimit = 300; // 5 mins
        if (Engine.dimLimit >= 0)
            Engine.dimLimit *= Engine.refreshRate;

        float bv = 0, sv = 0;
        if (!ini.GetFloat("Audio", "BGMVolume", &bv))
            bv = 1.0f;
        if (!ini.GetFloat("Audio", "SFXVolume", &sv))
            sv = 1.0f;

        bgmVolume = bv * MAX_VOLUME;
        sfxVolume = sv * MAX_VOLUME;

        if (bgmVolume > MAX_VOLUME)
            bgmVolume = MAX_VOLUME;
        if (bgmVolume < 0)
            bgmVolume = 0;

        if (sfxVolume > MAX_VOLUME)
            sfxVolume = MAX_VOLUME;
        if (sfxVolume < 0)
            sfxVolume = 0;

#if RETRO_USING_SDL2
        if (!ini.GetInteger("Keyboard 1", "Up", &inputDevice[INPUT_UP].keyMappings))
            inputDevice[INPUT_UP].keyMappings = SDL_SCANCODE_UP;
        if (!ini.GetInteger("Keyboard 1", "Down", &inputDevice[INPUT_DOWN].keyMappings))
            inputDevice[INPUT_DOWN].keyMappings = SDL_SCANCODE_DOWN;
        if (!ini.GetInteger("Keyboard 1", "Left", &inputDevice[INPUT_LEFT].keyMappings))
            inputDevice[INPUT_LEFT].keyMappings = SDL_SCANCODE_LEFT;
        if (!ini.GetInteger("Keyboard 1", "Right", &inputDevice[INPUT_RIGHT].keyMappings))
            inputDevice[INPUT_RIGHT].keyMappings = SDL_SCANCODE_RIGHT;
        if (!ini.GetInteger("Keyboard 1", "A", &inputDevice[INPUT_BUTTONA].keyMappings))
            inputDevice[INPUT_BUTTONA].keyMappings = SDL_SCANCODE_Z;
        if (!ini.GetInteger("Keyboard 1", "B", &inputDevice[INPUT_BUTTONB].keyMappings))
            inputDevice[INPUT_BUTTONB].keyMappings = SDL_SCANCODE_X;
        if (!ini.GetInteger("Keyboard 1", "C", &inputDevice[INPUT_BUTTONC].keyMappings))
            inputDevice[INPUT_BUTTONC].keyMappings = SDL_SCANCODE_C;
        if (!ini.GetInteger("Keyboard 1", "X", &inputDevice[INPUT_BUTTONX].keyMappings))
            inputDevice[INPUT_BUTTONX].keyMappings = SDL_SCANCODE_A;
        if (!ini.GetInteger("Keyboard 1", "Y", &inputDevice[INPUT_BUTTONY].keyMappings))
            inputDevice[INPUT_BUTTONY].keyMappings = SDL_SCANCODE_S;
        if (!ini.GetInteger("Keyboard 1", "Z", &inputDevice[INPUT_BUTTONZ].keyMappings))
            inputDevice[INPUT_BUTTONZ].keyMappings = SDL_SCANCODE_D;
        if (!ini.GetInteger("Keyboard 1", "L", &inputDevice[INPUT_BUTTONL].keyMappings))
            inputDevice[INPUT_BUTTONL].keyMappings = SDL_SCANCODE_Q;
        if (!ini.GetInteger("Keyboard 1", "R", &inputDevice[INPUT_BUTTONR].keyMappings))
            inputDevice[INPUT_BUTTONR].keyMappings = SDL_SCANCODE_E;
        if (!ini.GetInteger("Keyboard 1", "Start", &inputDevice[INPUT_START].keyMappings))
            inputDevice[INPUT_START].keyMappings = SDL_SCANCODE_RETURN;
        if (!ini.GetInteger("Keyboard 1", "Select", &inputDevice[INPUT_SELECT].keyMappings))
            inputDevice[INPUT_SELECT].keyMappings = SDL_SCANCODE_TAB;

        if (!ini.GetInteger("Controller 1", "Up", &inputDevice[INPUT_UP].contMappings))
            inputDevice[INPUT_UP].contMappings = SDL_CONTROLLER_BUTTON_DPAD_UP;
        if (!ini.GetInteger("Controller 1", "Down", &inputDevice[INPUT_DOWN].contMappings))
            inputDevice[INPUT_DOWN].contMappings = SDL_CONTROLLER_BUTTON_DPAD_DOWN;
        if (!ini.GetInteger("Controller 1", "Left", &inputDevice[INPUT_LEFT].contMappings))
            inputDevice[INPUT_LEFT].contMappings = SDL_CONTROLLER_BUTTON_DPAD_LEFT;
        if (!ini.GetInteger("Controller 1", "Right", &inputDevice[INPUT_RIGHT].contMappings))
            inputDevice[INPUT_RIGHT].contMappings = SDL_CONTROLLER_BUTTON_DPAD_RIGHT;
        if (!ini.GetInteger("Controller 1", "A", &inputDevice[INPUT_BUTTONA].contMappings))
            inputDevice[INPUT_BUTTONA].contMappings = SDL_CONTROLLER_BUTTON_A;
        if (!ini.GetInteger("Controller 1", "B", &inputDevice[INPUT_BUTTONB].contMappings))
            inputDevice[INPUT_BUTTONB].contMappings = SDL_CONTROLLER_BUTTON_B;
        if (!ini.GetInteger("Controller 1", "C", &inputDevice[INPUT_BUTTONC].contMappings))
            inputDevice[INPUT_BUTTONC].contMappings = SDL_CONTROLLER_BUTTON_X;
        if (!ini.GetInteger("Controller 1", "X", &inputDevice[INPUT_BUTTONX].contMappings))
            inputDevice[INPUT_BUTTONX].contMappings = SDL_CONTROLLER_BUTTON_Y;
        if (!ini.GetInteger("Controller 1", "Y", &inputDevice[INPUT_BUTTONY].contMappings))
            inputDevice[INPUT_BUTTONY].contMappings = SDL_CONTROLLER_BUTTON_ZL;
        if (!ini.GetInteger("Controller 1", "Z", &inputDevice[INPUT_BUTTONZ].contMappings))
            inputDevice[INPUT_BUTTONZ].contMappings = SDL_CONTROLLER_BUTTON_ZR;
        if (!ini.GetInteger("Controller 1", "L", &inputDevice[INPUT_BUTTONL].contMappings))
            inputDevice[INPUT_BUTTONL].contMappings = SDL_CONTROLLER_BUTTON_LEFTSHOULDER;
        if (!ini.GetInteger("Controller 1", "R", &inputDevice[INPUT_BUTTONR].contMappings))
            inputDevice[INPUT_BUTTONR].contMappings = SDL_CONTROLLER_BUTTON_RIGHTSHOULDER;
        if (!ini.GetInteger("Controller 1", "Start", &inputDevice[INPUT_START].contMappings))
            inputDevice[INPUT_START].contMappings = SDL_CONTROLLER_BUTTON_START;
        if (!ini.GetInteger("Controller 1", "Select", &inputDevice[INPUT_SELECT].contMappings))
            inputDevice[INPUT_SELECT].contMappings = SDL_CONTROLLER_BUTTON_GUIDE;

        if (!ini.GetFloat("Controller 1", "LStickDeadzone", &LSTICK_DEADZONE))
            LSTICK_DEADZONE = 0.3;
        if (!ini.GetFloat("Controller 1", "RStickDeadzone", &RSTICK_DEADZONE))
            RSTICK_DEADZONE = 0.3;
        if (!ini.GetFloat("Controller 1", "LTriggerDeadzone", &LTRIGGER_DEADZONE))
            LTRIGGER_DEADZONE = 0.3;
        if (!ini.GetFloat("Controller 1", "RTriggerDeadzone", &RTRIGGER_DEADZONE))
            RTRIGGER_DEADZONE = 0.3;
#endif

#if RETRO_USING_SDL1
        if (!ini.GetInteger("Keyboard 1", "Up", &inputDevice[INPUT_UP].keyMappings))
            inputDevice[INPUT_UP].keyMappings = SDLK_UP;
        if (!ini.GetInteger("Keyboard 1", "Down", &inputDevice[INPUT_DOWN].keyMappings))
            inputDevice[INPUT_DOWN].keyMappings = SDLK_DOWN;
        if (!ini.GetInteger("Keyboard 1", "Left", &inputDevice[INPUT_LEFT].keyMappings))
            inputDevice[INPUT_LEFT].keyMappings = SDLK_LEFT;
        if (!ini.GetInteger("Keyboard 1", "Right", &inputDevice[INPUT_RIGHT].keyMappings))
            inputDevice[INPUT_RIGHT].keyMappings = SDLK_RIGHT;
        if (!ini.GetInteger("Keyboard 1", "A", &inputDevice[INPUT_BUTTONA].keyMappings))
            inputDevice[INPUT_BUTTONA].keyMappings = SDLK_z;
        if (!ini.GetInteger("Keyboard 1", "B", &inputDevice[INPUT_BUTTONB].keyMappings))
            inputDevice[INPUT_BUTTONB].keyMappings = SDLK_x;
        if (!ini.GetInteger("Keyboard 1", "C", &inputDevice[INPUT_BUTTONC].keyMappings))
            inputDevice[INPUT_BUTTONC].keyMappings = SDLK_c;
        if (!ini.GetInteger("Controller 1", "X", &inputDevice[INPUT_BUTTONX].contMappings))
            inputDevice[INPUT_BUTTONX].contMappings = SDLK_a;
        if (!ini.GetInteger("Controller 1", "Y", &inputDevice[INPUT_BUTTONY].contMappings))
            inputDevice[INPUT_BUTTONY].contMappings = SDLK_s;
        if (!ini.GetInteger("Controller 1", "Z", &inputDevice[INPUT_BUTTONZ].contMappings))
            inputDevice[INPUT_BUTTONZ].contMappings = SDLK_d;
        if (!ini.GetInteger("Controller 1", "L", &inputDevice[INPUT_BUTTONL].contMappings))
            inputDevice[INPUT_BUTTONL].contMappings = SDLK_q;
        if (!ini.GetInteger("Controller 1", "R", &inputDevice[INPUT_BUTTONR].contMappings))
            inputDevice[INPUT_BUTTONR].contMappings = SDLK_e;
        if (!ini.GetInteger("Keyboard 1", "Start", &inputDevice[INPUT_START].keyMappings))
            inputDevice[INPUT_START].keyMappings = SDLK_RETURN;
        if (!ini.GetInteger("Keyboard 1", "Select", &inputDevice[INPUT_SELECT].keyMappings))
            inputDevice[INPUT_SELECT].keyMappings = SDLK_TAB;

        if (!ini.GetInteger("Controller 1", "Up", &inputDevice[INPUT_UP].contMappings))
            inputDevice[INPUT_UP].contMappings = 1;
        if (!ini.GetInteger("Controller 1", "Down", &inputDevice[INPUT_DOWN].contMappings))
            inputDevice[INPUT_DOWN].contMappings = 2;
        if (!ini.GetInteger("Controller 1", "Left", &inputDevice[INPUT_LEFT].contMappings))
            inputDevice[INPUT_LEFT].contMappings = 3;
        if (!ini.GetInteger("Controller 1", "Right", &inputDevice[INPUT_RIGHT].contMappings))
            inputDevice[INPUT_RIGHT].contMappings = 4;
        if (!ini.GetInteger("Controller 1", "A", &inputDevice[INPUT_BUTTONA].contMappings))
            inputDevice[INPUT_BUTTONA].contMappings = 5;
        if (!ini.GetInteger("Controller 1", "B", &inputDevice[INPUT_BUTTONB].contMappings))
            inputDevice[INPUT_BUTTONB].contMappings = 6;
        if (!ini.GetInteger("Controller 1", "C", &inputDevice[INPUT_BUTTONC].contMappings))
            inputDevice[INPUT_BUTTONC].contMappings = 7;
        if (!ini.GetInteger("Controller 1", "X", &inputDevice[INPUT_BUTTONX].contMappings))
            inputDevice[INPUT_BUTTONX].contMappings = 8;
        if (!ini.GetInteger("Controller 1", "Y", &inputDevice[INPUT_BUTTONY].contMappings))
            inputDevice[INPUT_BUTTONY].contMappings = 9;
        if (!ini.GetInteger("Controller 1", "Z", &inputDevice[INPUT_BUTTONZ].contMappings))
            inputDevice[INPUT_BUTTONZ].contMappings = 10;
        if (!ini.GetInteger("Controller 1", "L", &inputDevice[INPUT_BUTTONL].contMappings))
            inputDevice[INPUT_BUTTONL].contMappings = 11;
        if (!ini.GetInteger("Controller 1", "R", &inputDevice[INPUT_BUTTONR].contMappings))
            inputDevice[INPUT_BUTTONR].contMappings = 12;
        if (!ini.GetInteger("Controller 1", "Start", &inputDevice[INPUT_START].contMappings))
            inputDevice[INPUT_START].contMappings = 13;
        if (!ini.GetInteger("Controller 1", "Select", &inputDevice[INPUT_SELECT].contMappings))
            inputDevice[INPUT_SELECT].contMappings = 14;

        if (!ini.GetFloat("Controller 1", "LStickDeadzone", &LSTICK_DEADZONE))
            LSTICK_DEADZONE = 0.3;
        if (!}

    if (changedScreenWidth)
        SCREEN_XSIZE = SCREEN_XSIZE_CONFIG;
    changedScreenWidth = false;

    ReleaseRenderDevice(true);
    InitRenderDevice();

    for (int i = 1; i < TEXTURE_COUNT; ++i) {
        if (StrLength(textureList[i].fileName)) {
            char fileName[64];
            StrCopy(fileName, textureList[i].fileName);
            textureList[i].fileName[0] = 0;

            LoadTexture(fileName, textureList[i].format);
        }
    }

    for (int i = 0; i < MESH_COUNT; ++i) {
        if (StrLength(meshList[i].fileName)) {
            char fileName[64];
            StrCopy(fileName, meshList[i].fileName);
            meshList[i].fileName[0] = 0;

            LoadMesh(fileName, meshList[i].textureID);
        }
    }
}
#endif
