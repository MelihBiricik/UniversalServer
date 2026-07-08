namespace UniversalServer.Model
{
    public class SensorReadingTracker<T> where T : ValuesBase
    {
        public T Current { get; private set; }
        public T Min     { get; private set; }
        public T Max     { get; private set; }

        public void Update(T value)
        {
            Current = value;
            if (Min == null || value.Value < Min.Value) Min = value;
            if (Max == null || value.Value > Max.Value) Max = value;
        }

        public void Reset(T value)
        {
            Current = Min = Max = value;
        }
    }
}
