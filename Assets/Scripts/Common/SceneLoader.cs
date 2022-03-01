// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    private int _startMenuSceneIndex = 0;
    private int _createGameLobbyIndex = 1;
    private int _createGameSceneIndex = 2;
    private int _joinGameSceneIndex = 3;

    public void LoadStartMenuScene()
    {
        SceneManager.LoadScene(_startMenuSceneIndex);
    }

    public void LoadCreateGameScene()
    {
        SceneManager.LoadScene(_createGameSceneIndex);
    }

    public void LoadGameLobby()
    {
        SceneManager.LoadScene(_createGameLobbyIndex);
    }

    public void LoadJoinGameScene()
    {
        SceneManager.LoadScene(_joinGameSceneIndex);
    }
}
