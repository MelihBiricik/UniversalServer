namespace UniversalServer.Model
{
    public class Raum
    {
        public int RaumID { get; set; }
        public string Name { get; set; }

        public override string ToString() => Name;
    }
}
