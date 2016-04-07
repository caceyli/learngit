#!/usr/bin/perl
use strict;
use warnings;

$line0 = "(select MIN(CAT_CREATE_DATE) FROM (SELECT COALESCE(CAT_HW_MODEL_FDA.CREATE_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_CREATE_DATE union all SELECT COALESCE(CAT_FDA.CREATE_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_CREATE_DATE ) CAT_CREATE_DATE) as CAT_CREATE_DATE,";
$after0 = "LEAST(COALESCE(CAT_HW_MODEL_FDA.CREATE_DATE, CONVERT(DATETIME, '20000101', 120)),COALESCE(CAT_FDA.CREATE_DATE, CONVERT(DATETIME, '20000101', 120))) AS CAT_CREATE_DATE,";

$line = "(select Max(CAT_LAST_MODIFIED_DATE) FROM (SELECT COALESCE(CAT_HW_MODEL_FDA.LAST_MODIFIED_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_LAST_MODIFIED_DATE union all SELECT COALESCE(CAT_FDA.LAST_MODIFIED_DATE, CONVERT(DATETIME, '20000101', 120)) AS CAT_LAST_MODIFIED_DATE ) CAT_LAST_MODIFIED_DATE) as CAT_LAST_MODIFIED_DATE";
$after = "GREATEST(COALESCE(CAT_HW_MODEL_FDA.LAST_MODIFIED_DATE, CONVERT(DATETIME, '20000101', 120)),COALESCE(CAT_FDA.LAST_MODIFIED_DATE, CONVERT(DATETIME, '20000101', 120))) AS CAT_LAST_MODIFIED_DATE,";

print "$line\n";
if ($line=~m/(min|max)/gi){
$fun = $1;
}
$fun = uc($fun);
print "funtion is: $fun\n";
%h=('MIN','LEAST','MAX','GREATEST');
print "$fun is $h{$fun}\n";

$a= ($line=~m/(min|max)\((.+?)\)/gi);
if ($line=~m/(min|max)\((.+?)\)/gi){
$colname = $2;
}
print "column name is: $colname\n";
while($line=~m/(COALESCE.+?)\s+AS/g)
{
 push @colist,$1;
}
print "column list is : @colist\n\n";
foreach(@colist){
printf "$_\n";
}
$line = join (",",@colist);
printf "$h{$fun}\($line\) AS $colname,\n";


