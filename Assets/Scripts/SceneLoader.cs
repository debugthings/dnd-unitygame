// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    private int _startMenuSceneIndex = 0;
    private int _createGameSceneIndex = 1;
    private int _joinGameSceneIndex = 2;

    public void LoadStartMenuScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(_startMenuSceneIndex);
    }

    public void LoadCreateGameScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(_createGameSceneIndex);
    }

    public void LoadJoinGameScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(_joinGameSceneIndex);
    }
}
