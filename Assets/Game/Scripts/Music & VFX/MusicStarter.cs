using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicStarter : MonoBehaviour
{
    public static MusicStarter Instance { get; private set; }

    [SerializeField] private float _startDelay = 0.5f;
    [SerializeField] private float _switchFadeDuration = 1f;

    // AJOUTE (2026-09-15) - retour utilisateur : un arret/demarrage NET (sans
    // fondu du tout) s'entend comme une coupure desagreable ("effet de coupe
    // pas bien"), meme tres bref. Fondus courts par defaut plutot qu'un
    // Stop()/Play() sec, mais volontairement BREFS (pas comme
    // _switchFadeDuration, pense pour un crossfade ample entre 2 morceaux) -
    // l'objectif ici est de rester quasi instantane a l'oreille tout en
    // evitant le clic audio, et surtout de laisser le debut du morceau
    // clairement audible plutot que noye dans un long fondu d'entree.
    [SerializeField] private float _startFadeDuration = 0.3f;
    [SerializeField] private float _stopFadeDuration = 0.2f;

    // AJOUTE - petit delai avant meme de commencer le fondu de transition, pour
    // eviter que le changement de musique semble se declencher pile au moment
    // du trigger (spawn/mort du boss) - laisse un instant de respiration avant
    // que quoi que ce soit ne bouge au niveau du son.
    [SerializeField] private float _switchDelay = 0.5f;

    private AudioSource _audioSource;
    private AudioClip _defaultClip;
    private Coroutine _fadeCoroutine;
    private bool _started = false;
    // AJOUTÉ (2026-10-18, diagnostic complet - audit §2.9) - volume "normal" de l'AudioSource (le son réel du
    // joueur passe par le Mixer, cette valeur n'est qu'un point de référence interne pour les fondus de CE script).
    // Mémorisé UNE SEULE FOIS ici, avant le moindre fondu : SwitchRoutine() lisait auparavant _audioSource.volume
    // AU MOMENT DU SWITCH pour savoir jusqu'où remonter après le fondu - si un 2e changement de musique arrivait
    // pendant que le 1er fondu était encore en cours (ex: boss tué moins de 1,5s après son apparition), ce 2e appel
    // capturait un volume DÉJÀ PARTIELLEMENT BAISSÉ comme "volume cible", et la musique restait bloquée à ce volume
    // réduit jusqu'au prochain chargement de scène.
    private float _targetVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        _defaultClip = _audioSource.clip; // memorise la musique normale assignee dans l'Inspector
        _targetVolume = _audioSource.volume;
    }

    private void Start()
    {
        StartCoroutine(StartWhenAudioReady());
    }

    // MODIFIE (2026-09-26) - retour utilisateur : la musique de l'écran de chargement doit être calée
    // directement sur le volume réglé dans les Paramètres lors de la dernière session. Le mixer se
    // charge en asynchrone (SettingsApplier.LoadMixer) : on attend que les volumes sauvegardés soient
    // appliqués avant de lancer le morceau (garde-fou de 3 s réelles si le mixer est introuvable).
    private IEnumerator StartWhenAudioReady()
    {
        yield return new WaitForSecondsRealtime(_startDelay);

        float waited = 0f;
        while (!SettingsApplier.AudioReady && waited < 3f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        PlayMusic();
    }

    // MODIFIE (2026-09-15) - demarre desormais avec un fondu d'entree court
    // (_startFadeDuration) au lieu d'un Play() a plein volume d'un coup :
    // couvre TOUT demarrage de musique (menu au premier chargement, musique
    // de la partie au debut d'une run) qui sonnait sec sinon.
    private void PlayMusic()
    {
        if (_audioSource == null) return;

        _started = true;
        _audioSource.volume = 0f;
        _audioSource.Play();

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeInRoutine(_targetVolume, _startFadeDuration));
    }

    public void SwitchTo(AudioClip newClip)
    {
        if (newClip == null || _audioSource.clip == newClip) return;

        // CORRIGE (2026-09-26) - un boss présent dès le début de la partie (Choc des titans) demandait sa musique AVANT que la
        // musique de départ (lancée après _startDelay / l'attente du mixer) ne démarre : PlayMusic() annulait alors ce
        // changement et jouait le morceau normal. Tant que rien n'a démarré, on remplace simplement le morceau à lancer.
        if (!_started)
        {
            _audioSource.clip = newClip;
            _audioSource.loop = true;
            return;
        }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(SwitchRoutine(newClip));
    }

    // AJOUTÉ (2026-11-21, arrivée des boss - BossIntro) - lance un morceau d'un coup, à vitesse normale, après le silence de
    // l'arrivée (la musique d'avant a déjà été éteinte par FadeOutAndStop). Fondu de 0,08 s seulement : évite le clic audio
    // sans adoucir l'attaque. Clip nul : la musique normale de la partie reprend (jamais de combat sans musique).
    public void PlayNow(AudioClip clip)
    {
        if (_audioSource == null) return;
        if (clip == null) clip = _defaultClip;
        if (clip == null) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        if (_pitchCoroutine != null) { StopCoroutine(_pitchCoroutine); _pitchCoroutine = null; }
        _started = true;
        _audioSource.pitch = 1f;
        _audioSource.clip = clip;
        _audioSource.loop = true;
        _audioSource.volume = 0f;
        _audioSource.Play();
        _fadeCoroutine = StartCoroutine(FadeInRoutine(_targetVolume, 0.08f));
    }

    public void RevertToDefault()
    {
        SwitchTo(_defaultClip);
    }

    // AJOUTÉ (2026-11-14, Frénésie de l'Escarmouche) - accélère (ou ramène) la musique en douceur. Le pitch d'une
    // AudioSource change aussi la hauteur : rester sous ~1,15 pour que le morceau reste reconnaissable.
    private Coroutine _pitchCoroutine;

    public void SetPitch(float target, float duration = 1.5f)
    {
        if (_audioSource == null) return;
        if (_pitchCoroutine != null) StopCoroutine(_pitchCoroutine);
        _pitchCoroutine = StartCoroutine(PitchRoutine(target, duration));
    }

    // AJOUTÉ (2026-11-18, retour utilisateur) - fin de Frénésie suivie d'un boss : on NE ramène PAS la vitesse tout de suite (le
    // morceau normal repassait à x1 pendant ~1 s avant que celui du boss ne démarre) ; la musique de Frénésie fond encore
    // accélérée, et celui du boss s'ouvre à x1 (SwitchRoutine). Filet de sécurité : si aucun changement de morceau n'a eu lieu
    // après `delay` s réelles (boss sans musique propre), retour progressif à x1.
    public void ResetPitchLater(float delay)
    {
        StartCoroutine(ResetPitchLaterRoutine(delay));
    }

    private IEnumerator ResetPitchLaterRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (!GameModes.FrenzyActive && !Mathf.Approximately(_audioSource.pitch, 1f)) SetPitch(1f, 0.5f);
    }

    private IEnumerator PitchRoutine(float target, float duration)
    {
        float start = _audioSource.pitch;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.pitch = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        _audioSource.pitch = target;
        _pitchCoroutine = null;
    }

    public void FadeOutAndStop(float duration)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutRoutine(duration));
    }

    // MODIFIE (2026-09-15) - arret RAPIDE mais toujours en fondu
    // (_stopFadeDuration, tres court) plutot qu'un Stop() sec : un arret net,
    // meme instantane, s'entendait comme une coupure desagreable (retour
    // utilisateur). Le fondu est assez bref pour rester silencieux bien avant/
    // pendant l'ecran noir de SceneTransitionFader (~0.25s), sans le clic
    // audio d'un Stop() brut.
    public void Stop()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutRoutine(_stopFadeDuration));
    }

    private IEnumerator SwitchRoutine(AudioClip newClip)
    {
        // AJOUTE - WaitForSecondsRealtime plutot qu'un timer classique : reste
        // correct meme si Time.timeScale est modifie ailleurs au meme moment
        // (ex. le hitstop de l'ultime dans CrystalSystem).
        yield return new WaitForSecondsRealtime(_switchDelay);

        float startVolume = _audioSource.volume;
        float elapsed = 0f;
        float halfDuration = _switchFadeDuration * 0.5f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
            yield return null;
        }

        _audioSource.clip = newClip;
        // AJOUTÉ (2026-11-18) - le nouveau morceau s'ouvre à sa vitesse propre (x1), jamais à celle de la Frénésie : voir
        // ResetPitchLater (fin de Frénésie = le boss enchaîne, l'ancien morceau reste accéléré jusqu'au fondu).
        if (_pitchCoroutine != null) { StopCoroutine(_pitchCoroutine); _pitchCoroutine = null; }
        _audioSource.pitch = GameModes.FrenzyActive ? GameModes.FrenzyMusicPitch : 1f;
        // AJOUTE - force le loop explicitement au moment du switch, plutot que
        // de dependre de l'etat du composant dans l'Inspector au moment ou tu
        // lis ce commentaire - garanti quel que soit le prefab/la config.
        _audioSource.loop = true;
        _audioSource.Play();

        // CORRIGÉ (2026-10-18) - remonte vers _targetVolume (fixé une fois pour toutes dans Awake), jamais vers
        // startVolume : ce dernier n'est fiable que pour la moitié "fondu vers le bas" ci-dessus (on part bien d'où
        // le volume se trouve réellement maintenant, même interrompu), pas pour la cible finale.
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(0f, _targetVolume, elapsed / halfDuration);
            yield return null;
        }

        _audioSource.volume = _targetVolume;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVolume = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        _audioSource.volume = 0f;
        _audioSource.Stop();
    }

    // AJOUTE (2026-09-15) - pendant du FadeOutRoutine ci-dessus, pour
    // PlayMusic(). L'AudioSource doit deja etre en lecture (volume=0) avant
    // d'appeler cette coroutine - voir PlayMusic().
    private IEnumerator FadeInRoutine(float targetVolume, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
            yield return null;
        }

        _audioSource.volume = targetVolume;
    }
}