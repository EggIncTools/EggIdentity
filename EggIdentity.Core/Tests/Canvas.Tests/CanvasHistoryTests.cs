namespace EggIdentity.Canvas.Tests;

public class CanvasHistoryTests {
    private static CanvasDoc Doc(int cols) => new(cols, 2, []);

    [Fact]
    public void UndoAndRedoWalkTheStack() {
        var history = new CanvasHistory(Doc(1));
        history.Push(Doc(2));
        history.Push(Doc(3));

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(2, history.Undo().Cols);
        Assert.Equal(1, history.Undo().Cols);
        Assert.False(history.CanUndo);
        Assert.Equal(1, history.Undo().Cols);
        Assert.Equal(2, history.Redo().Cols);
        Assert.Equal(3, history.Redo().Cols);
        Assert.Equal(3, history.Redo().Cols);
    }

    [Fact]
    public void PushClearsTheRedoBranch() {
        var history = new CanvasHistory(Doc(1));
        history.Push(Doc(2));
        history.Undo();
        history.Push(Doc(9));

        Assert.False(history.CanRedo);
        Assert.Equal(9, history.Current.Cols);
    }

    [Fact]
    public void LimitDropsTheOldest() {
        var history = new CanvasHistory(Doc(0), limit: 2);
        history.Push(Doc(1));
        history.Push(Doc(2));
        history.Push(Doc(3));

        Assert.Equal(2, history.Undo().Cols);
        Assert.Equal(1, history.Undo().Cols);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void PushingAnEqualDocIsANoOp() {
        var history = new CanvasHistory(Doc(1));
        history.Push(Doc(1));

        Assert.False(history.CanUndo);
    }
}
