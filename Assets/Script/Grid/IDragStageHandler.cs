public interface IDragStateHandler
{
    void BeginDragState();
    void EndDragState(bool committed);
}