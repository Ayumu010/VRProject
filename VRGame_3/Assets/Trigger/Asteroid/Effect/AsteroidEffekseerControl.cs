
using System.Collections;
using UnityEngine;
using Effekseer;

public static class AsteroidEffekseerControl
{
    /// <summary>
    /// 再生して指定フレーム後に一時停止する。
    /// </summary>
    public static IEnumerator PlayAndPause(
        EffekseerEmitter emitter,
        int frames)
    {
        if (emitter == null)
        {
            Debug.LogError("EffekseerEmitterが設定されていません。");
            yield break;
        }

        emitter.paused = false;
        emitter.Stop();
        emitter.Play();

        for (int i = 0; i < Mathf.Max(1, frames); i++)
        {
            yield return null;
        }

        if (emitter == null)
            yield break;

        emitter.paused = true;
    }

    /// <summary>
    /// 再生して指定秒数後に完全停止する。
    /// </summary>
    public static IEnumerator PlayForSecondsAndStop(
        EffekseerEmitter emitter,
        float duration)
    {
        if (emitter == null)
        {
            Debug.LogError("EffekseerEmitterが設定されていません。");
            yield break;
        }

        emitter.paused = false;
        emitter.Stop();
        emitter.Play();

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, duration)
        );

        if (emitter == null)
            yield break;

        emitter.paused = false;
        emitter.Stop();
    }
}
