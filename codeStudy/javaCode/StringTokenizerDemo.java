package caceyJava;

import java.util.StringTokenizer;


public class StringTokenizerDemo {
   public static void main(String[] args) {
       // creating string tokenizer
       StringTokenizer st = new StringTokenizer("Tutorialspoint.is.the.best.site",".");
       // counting tokens
       System.out.println("Total tokens : " + st.countTokens());     
  	   while (st.hasMoreTokens()) {
		   System.out.println(st.nextToken());
	   }
   }
}