#open ann existed file, and read its data line by line. 
#!/usr/bin/perl

open(GRADES, "test.txt") or die "Can't open grades: $!\n";

while (chomp($line = <GRADES>)) {
#      @fields = split (/:/,$line);
       #$line=chomp($line);
       print "$line\n";
       ($name,$age)= split(/:/,$line);
       print "show:$name\{$age\}\n";

}
close(GRADES);

#open an existed file, add string $a at the end of the file.
#!/usr/bin/perl
open (input,">>test.txt");

$a="hello,world!";
print input  "$a";
close(input);


#create test.txt, read from constant file line by line, deal with regular expression and write the result to test.txt.
#!/usr/bin/perl
open input, ">test.txt";

while(defined($strl =<>))
{
$strl=~/(\|.*\|)/;
print input $1;
}
20150900
