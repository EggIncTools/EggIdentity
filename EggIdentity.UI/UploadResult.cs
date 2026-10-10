namespace EggIdentity.UI;

public sealed record UploadResult(int Status, string Body) {
    public bool Ok => Status is >= 200 and < 300;
}
