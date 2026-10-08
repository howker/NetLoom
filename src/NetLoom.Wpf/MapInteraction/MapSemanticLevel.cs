namespace NetLoom.Wpf.MapInteraction
{
    // Издалека: размещения и важные имена экранного размера; средний: карточки устройств.
    // Вблизи: порты связей; подробно: адрес и модель устройства, известная скорость связи.
    // Дальний порог совпадает с порогом читаемости карточек; границы задаются токенами.
    public enum MapSemanticLevel { Far, Medium, Close, Detailed }

    public static class MapSemanticLevels
    {
        public static MapSemanticLevel For(double zoom, double farMax, double labelMin, double detailMin)
        {
            if (zoom < farMax) return MapSemanticLevel.Far;
            if (zoom < labelMin) return MapSemanticLevel.Medium;
            if (zoom < detailMin) return MapSemanticLevel.Close;
            return MapSemanticLevel.Detailed;
        }
    }
}
