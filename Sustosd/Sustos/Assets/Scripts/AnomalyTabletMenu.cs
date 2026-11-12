using UnityEngine;
using UnityEngine.UI;

public sealed class AnomalyTabletMenu : MonoBehaviour
{
    private Text statusText;
    private string selectedCategory;

    private void Awake()
    {
        foreach (Text label in GetComponentsInChildren<Text>(true))
        {
            if (label.gameObject.name == "TabletStatus")
            {
                statusText = label;
                break;
            }
        }

        if (statusText == null)
        {
            Debug.LogError("La interfaz de la tablet no tiene un texto llamado TabletStatus.", this);
            return;
        }

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            switch (button.gameObject.name)
            {
                case "Category_Object":
                    button.onClick.AddListener(() => SelectCategory("Objeto alterado"));
                    break;
                case "Category_Presence":
                    button.onClick.AddListener(() => SelectCategory("Presencia"));
                    break;
                case "Category_Sound":
                    button.onClick.AddListener(() => SelectCategory("Sonido extraño"));
                    break;
                case "Category_Other":
                    button.onClick.AddListener(() => SelectCategory("Otra anomalía"));
                    break;
                case "SendReport":
                    button.onClick.AddListener(SendReport);
                    break;
            }
        }
    }

    private void SelectCategory(string category)
    {
        selectedCategory = category;
        statusText.text = "Seleccionado: " + category;
    }

    private void SendReport()
    {
        if (string.IsNullOrEmpty(selectedCategory))
        {
            statusText.text = "Selecciona primero una anomalía.";
            return;
        }

        statusText.text = "Reporte enviado: " + selectedCategory;
        Debug.Log("Anomalía reportada: " + selectedCategory, this);
    }
}
