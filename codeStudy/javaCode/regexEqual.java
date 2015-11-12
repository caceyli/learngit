import java.util.regex.Matcher;
import java.util.regex.Pattern;
public class test
{
    public static void main(String[] args) 
    {
        System.out.println("Hello World!");
        String line = "true";
        Pattern p1 = Pattern.compile("^true$", Pattern.CASE_INSENSITIVE);
        Matcher m1 = p1.matcher(line);
        if (m1.find())
        {
            System.out.println("match!");
        }

    }
}
