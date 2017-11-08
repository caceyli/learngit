BDNA-21317: Scan Task With Defined Scan Allowed Window Does Not Run
https://bdnacorp.atlassian.net/browse/BDNA-21317

After reviewing the related platform code for scan task and many tests, it was figured out why this issue happened. Here is a brief conclusion for this deeper research:
1. The issue of this bug:
Among all the Scan tasks (L1/L2) with defined 'Scan Allow Time (specific times with option "Work Hour(s), Weekday(s), Weekend(s)" on UI)', only the first one can start normally, others are Out of Shift and not start at all.
2. What caused this issue:
The calendar shift of those tasks(except the first task) were not generated at all, resulting in this issue, making all tasks that should have been in shift do not start at all(Out Of Shift). And the new calendar were not created Because:
a. All those Scan tasks (L1/L2) with defined 'Scan Allow Time' have the same shiftPattern (eg: all task with scan option Weekday(s), have the same shiftPattern "weekly;1;mon,tue,wed,thu,fri").
b. And BNDA code generate the new calendar shift for new task based on the shiftPattern, it remembers the first shiftPatter(first task) in cache when first task were created, while there are new tasks coming up, it will read the cache firstly to decide whether the calendar for this shiftPatter had been created, if yes, it will skip this. That's why all tasks (with option "Weekday(s)" Scan Allow Time) after the first one can't start normally.
3. How to fix this issue:
This issue can be fixed by enhancing the code for generating new calendar for new tasks that have same shiftPatter.

4. related sqls to check the task shifts:
select * from am_ls_to_cal;
select DISTINCT ls_id from am_cal_future;

5. code:
$BDNA_SOURCE/bdna/com/bdna/si/db/ScanTaskStateMachine.java
$BDNA_SOURCE/bdna/com/bdna/si/db/DBScanTask.java
$BDNA_SOURCE/bdna/com/bdna/agenda/AMLsCalMgr.java (no code change)
$BDNA_SOURCE/bdna/com/bdna/agenda/CalendarFutureMgr.java

    /** Get generator from cache or make a new one if needed */
    private CalGen getCalGen(String pattern) throws BDNAException {
        CalGen calGen = (CalGen)m_patternToGenerator.get(pattern);
        if (null == calGen) {
            calGen = makeCalGen(pattern);
            m_patternToGenerator.put(pattern, calGen);
        }
        return calGen;
    }


    /** Get generator from cache or make a new one if needed */
    private CalGen getCalGen(String pattern) throws BDNAException {
        CalGen calGen = (CalGen)m_patternToGenerator.get(pattern);
        /*BDNA-21317: if the pattern contains 'weekly', create a new generator as well.*/
        if (null == calGen || pattern.contains("weekly")) {
            calGen = makeCalGen(pattern);
            m_patternToGenerator.put(pattern, calGen);
        }
        return calGen;
    }

6. reference: 
Column 6: Shift
Defines the times during each day when collections are enabled for a particular Scan Task. It has the following syntax:
shifts ::= shiftDefinition{+shiftDefinition}*
shiftDefinition ::= always | shiftType;hh1:mm1;hh2:mm2;shiftPattern
shiftType ::= daily | nights | workHours | weekdays | weekend
Appendix A, Working with Bulk Load Files Scan Tasks
BDNA Discover 7.7.0 Administrator Guide — Confidential and Proprietary to BDNA 184
shiftPattern ::= daily;<n> | weekly;<n>;day{,day}*
day ::= mon | tue | wed | thu | fri | sat | sun

example
Inventory       Basic Scan - Level 1 (no credentials required)  root.$bdna.project_Project_All4 immediately     12/31/2132 00:00:00     weekend;15:00;16:30;weekly;1;sat,sun    once            Scan Task All Level1            Normal Scan


7. related java code test:
package caceyJava;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.HashMap;
import java.util.StringTokenizer;

import javax.swing.text.html.HTMLDocument.Iterator;

public class scanTask {
    public static void main(String[] args) 
    {:q
    	String weekDays="mon,tue,wed,thu";
    	ArrayList m_weekDays;
    	ArrayList<Integer> weekDaysList = new ArrayList();
    	HashMap m_weekDay = new HashMap();
    	
    	String[][] s_weekDay = {{"sun", "Sun", "SUN", "sunday", "Sunday", "SUNDAY", "0"},
                {"mon", "Mon", "MON", "monday", "Monday", "MONDAY", "1"},
                {"tue", "Tue", "TUE", "tuesday", "Tuesday", "TUESDAY", "2"},
                {"wed", "Wed", "WED", "wednesday", "Wednesday", "WEDNESDAY", "3"},
                {"thu", "Thu", "THU", "thur", "Thur", "THUR", "thursday", "Thursday", "THURSDAY", "4"},
                {"fri", "Fri", "FRI", "friday", "Friday", "FRIDAY", "5"},
                {"sat", "Sat", "SAT", "saturday", "Saturday", "SATURDAY", "6"},
            };

    	int [] s_gregorianDay = {Calendar.SUNDAY,
                Calendar.MONDAY,
                Calendar.TUESDAY,
                Calendar.WEDNESDAY,
                Calendar.THURSDAY,
                Calendar.FRIDAY,
                Calendar.SATURDAY
                };

        for (int i = 0; i < s_weekDay.length; i++) {
            for (int j = 0; j < s_weekDay[i].length; j++) {
				m_weekDay.put(s_weekDay[i][j], new Integer(s_gregorianDay[i]));
            }
        }

    	StringTokenizer st = new StringTokenizer(weekDays, ",");
        System.out.println("Hello World!111");

        while (st.hasMoreTokens()) {
            Integer gregorianDay = null;
            String day = st.nextToken();

            gregorianDay = (Integer) m_weekDay.get(day.toLowerCase().trim());
            if (gregorianDay != null) {
                weekDaysList.add(gregorianDay);
            }            
        }
        System.out.println(weekDaysList);
        m_weekDays = weekDaysList;   
        int m_days[]; 
        Calendar m_currentWeekStartDate;
        m_currentWeekStartDate = null;

        // put the days in a vector for easy rotation
        m_days = new int[8];
        for (java.util.Iterator iter = m_weekDays.iterator(); iter.hasNext();) {
            Integer day = (Integer) iter.next();
            System.out.println(day.intValue());           
            m_days[day.intValue()] = 1;
        }
        
        System.out.println(m_days[2]);
    }
}


result:
Hello World!111
[2, 3, 4, 5]
2
3
4
5
1

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
        
        String calGen = "aa";
        String shiftpattern="weekl;1;mon,tue,wed,thu,fri";
        if ("aa" == calGen || shiftpattern.contains("weekly")) {
        	System.out.println("weekly!");
        }        
    }
}

result:
Hello World!
match!
weekly!


