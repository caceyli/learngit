package caceyJava;

import java.util.regex.Matcher;
import java.util.regex.Pattern;
public class checkEqual
{
    public static void main(String[] args) 
    {
        System.out.println("Hello World!");
        String line = "TRUE";
        //String info;
        //Pattern p1 = Pattern.compile("^true$", Pattern.CASE_INSENSITIVE);
        Pattern p1 = Pattern.compile("(^true$)|(^TRUE$)");
        Matcher m1 = p1.matcher(line);
        if (m1.find())
        {
            System.out.println("match!");
            //info = m1.group(1);
        }

    }
}
