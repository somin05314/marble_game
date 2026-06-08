using System.Collections;
using TMPro;
using UnityEngine;

public class SimpleToastUI : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] TMP_Text messageText;
    [SerializeField] float showDuration = 1.5f;

    Coroutine _co;

    public void Show(string message)
    {
        if (_co != null)
            StopCoroutine(_co);

        _co = StartCoroutine(CoShow(message));
    }

    IEnumerator CoShow(string message)
    {
        if (root != null) root.SetActive(true);
        if (messageText != null) messageText.text = message;

        yield return new WaitForSeconds(showDuration);

        if (root != null) root.SetActive(false);
        _co = null;
    }
}