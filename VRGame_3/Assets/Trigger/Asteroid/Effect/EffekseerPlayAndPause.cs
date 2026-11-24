
using System.Collections;
using UnityEngine;
using Effekseer;

public class EffekseerPlayAndPause : MonoBehaviour
{
    [SerializeField]
    private EffekseerEmitter emitter;

    [SerializeField, Min(1)]
    private int frames = 3;

    private void Start()
    {
        if (emitter == null)
            emitter = GetComponent<EffekseerEmitter>();

        if (emitter == null)
        {
            Debug.LogError("EffekseerEmitter‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñB", this);
            return;
        }

        StartCoroutine(PlayEffect());
    }

    private IEnumerator PlayEffect()
    {
        emitter.paused = false;
        emitter.Play();

        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }

        emitter.paused = true;
    }
}
