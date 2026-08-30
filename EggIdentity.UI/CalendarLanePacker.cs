namespace EggIdentity.UI;

public static class CalendarLanePacker {
    public static int AssignLane(List<double> laneRights, double left, double right, double gapFraction) {
        for (int i = 0; i < laneRights.Count; i++) {
            if (laneRights[i] - gapFraction <= left) {
                laneRights[i] = right;
                return i;
            }
        }
        laneRights.Add(right);
        return laneRights.Count - 1;
    }
}
