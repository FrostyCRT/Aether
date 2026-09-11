using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AmbientDustUI : MonoBehaviour
{
    [Header("Apparence")]
    [SerializeField] private Sprite _particleSprite; // un cercle doux ; le sprite "Knob" par defaut d'Unity fonctionne
    [SerializeField] private int _particleCount = 45;
    [SerializeField] private Color _particleColor = new Color(1f, 0.88f, 0.6f, 1f); // MODIFIE - alpha de base remonte a 1 (etait 0.8), le detail du fond mangeait le peu de transparence deja presente

    [Header("Zone verticale (0 = bas du conteneur, 1 = haut)")]
    // AJOUTE - confine desormais les particules a une bande precise au lieu de
    // traverser tout l'ecran. Sur ce fond tres charge au centre, les particules
    // qui passaient par la zone des personnages/loups s'y perdaient visuellement
    // ; les concentrer sur les zones calmes (haut pres du logo, bas pres de la
    // barre) les rend beaucoup plus visibles pour le meme nombre de particules.
    [SerializeField] private float _bandMinY = 0f;
    [SerializeField] private float _bandMaxY = 0.3f;

    [Header("Mouvement")]
    [SerializeField] private float _swayAmplitude = 15f;
    [SerializeField] private float _swayFrequency = 0.4f;

    [Header("Taille et duree de vie")]
    [SerializeField] private float _minSize = 5f; // MODIFIE - etait 4
    [SerializeField] private float _maxSize = 13f; // MODIFIE - etait 10
    [SerializeField] private float _minLifetime = 6f;
    [SerializeField] private float _maxLifetime = 14f;

    private RectTransform _rect;
    private readonly List<DustParticle> _particles = new List<DustParticle>();

    private class DustParticle
    {
        public RectTransform rect;
        public Image image;
        public float swayPhase;
        public float lifetime;
        public float age;
        public float startX;
    }

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        for (int i = 0; i < _particleCount; i++)
        {
            CreateParticle();
            _particles[i].age = Random.Range(0f, _particles[i].lifetime);
        }
    }

    private void CreateParticle()
    {
        GameObject go = new GameObject("DustMote", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_rect, false);

        Image img = go.GetComponent<Image>();
        img.sprite = _particleSprite;
        img.color = _particleColor;
        img.raycastTarget = false;

        float size = Random.Range(_minSize, _maxSize);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        _particles.Add(new DustParticle
        {
            rect = rt,
            image = img,
            swayPhase = Random.Range(0f, Mathf.PI * 2f),
            lifetime = Random.Range(_minLifetime, _maxLifetime),
            startX = Random.Range(0f, _rect.rect.width) - _rect.rect.width * 0.5f,
            age = 0f
        });
    }

    private void Update()
    {
        float height = _rect.rect.height;

        // MODIFIE - la bande remplace le calcul base sur _minSpeed/_maxSpeed :
        // chaque particule parcourt exactement bandBottom -> bandTop sur la
        // duree de sa propre vie, au lieu de traverser tout le conteneur a une
        // vitesse arbitraire.
        float bandBottom = -height * 0.5f + _bandMinY * height;
        float bandTop = -height * 0.5f + _bandMaxY * height;

        foreach (DustParticle p in _particles)
        {
            p.age += Time.deltaTime;
            float t = p.age / p.lifetime;

            if (t >= 1f)
            {
                p.age = 0f;
                t = 0f;
                p.swayPhase = Random.Range(0f, Mathf.PI * 2f);
                p.lifetime = Random.Range(_minLifetime, _maxLifetime);
                p.startX = Random.Range(0f, _rect.rect.width) - _rect.rect.width * 0.5f;
            }

            float y = Mathf.Lerp(bandBottom, bandTop, t);
            float sway = Mathf.Sin(p.age * _swayFrequency * Mathf.PI * 2f + p.swayPhase) * _swayAmplitude;
            p.rect.anchoredPosition = new Vector2(p.startX + sway, y);

            float alpha = t < 0.15f ? t / 0.15f : (t > 0.85f ? (1f - t) / 0.15f : 1f);
            Color c = p.image.color;
            c.a = alpha * _particleColor.a;
            p.image.color = c;
        }
    }
}