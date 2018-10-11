using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour {

    MusicPlayer musicPlayer;
    public static bool isPaused;
      

    public void LoadLevel(string name){                

        musicPlayer = FindObjectOfType<MusicPlayer>();
        if (musicPlayer)
        {
            musicPlayer.SetLevelMusic(name);
        }

		SceneManager.LoadScene(name);	
	}

    public void PauseGame(GameObject menuCanvas)
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        LevelManager.isPaused = true;

        menuCanvas.SetActive(true);
        Time.timeScale = 1f;        
    }

    public void ResumeGame()
    {
        GameObject.Find("MenuCanvas").SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        LevelManager.isPaused = false;
                
        Time.timeScale = 1f;        
    }

    public void ToggleDebug()
    {
        SettingsStatic.debug = !SettingsStatic.debug;
    }

    public void QuitGame(){
		Debug.Log("Quit Requested");
		Application.Quit();		
	}

	public void LoadNextLevel(){
        /* musicPlayer = FindObjectOfType<MusicPlayer>();
        musicPlayer.SetLevelMusic(SceneManager.GetActiveScene().buildIndex + 1); */

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);	
	}

    
}
