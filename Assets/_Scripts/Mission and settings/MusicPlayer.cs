using UnityEngine;
using System.Collections;

public class MusicPlayer : MonoBehaviour {
	// Use this for initialization

	static MusicPlayer instance = null;

	public AudioClip[] musicClips;	

	private AudioSource musicSource;

	void Awake(){
	
	}

	void Start () {
		if (instance != null && instance != this) {
			Destroy (gameObject);
		} else {
			GameObject.DontDestroyOnLoad (gameObject);
			instance = this;
			musicSource = GetComponent<AudioSource>();
            musicSource.spatialize = false;
            musicSource.clip = musicClips[0];
			musicSource.loop = true;
			musicSource.Play();
		}
	}

	public void SetLevelMusic(string level){        

        switch (level)
        {
            case "00 Splash":
                musicSource.clip = musicClips[0];
                break;
            case "01a Start":
                if(musicSource.clip != musicClips[0])
                {
                    musicSource.Stop();
                    musicSource.clip = musicClips[0];
                }
                break;
            case "01b Settings":
                break;
            case "01c Briefing":
                break;
            case "02 Game":
                musicSource.Stop();
                musicSource.clip = musicClips[1];
                break;
            case "03a Win":
                musicSource.Stop();
                musicSource.clip = musicClips[2];
                break;
            case "03b Lose":
                musicSource.Stop();
                musicSource.clip = musicClips[2];
                break;
        }
        
        musicSource.loop = true;
        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }

    }
	
	// Update is called once per frame
	void Update () {
        musicSource.volume = SettingsManager.GetMusicVolume();
	}
}
