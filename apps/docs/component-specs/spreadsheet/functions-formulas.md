---
title: Functions and Formulas
page_title: Spreadsheet - Functions and Formulas
description: Learn about all built-in functions supported by the Sunfish UI for Blazor Spreadsheet.
slug: spreadsheet-functions-formulas
tags: sunfish,blazor,spreadsheet
published: True
position: 30
components: ["spreadsheet"]
---
# Spreadsheet Functions and Formulas

This article lists the built-in functions and formulas supported by the Spreadsheet for Blazor.


## Formulas

A Spreadsheet formula starts with the equal sign `=` and can include:

* References to cells and ranges
* Constants and literals
* Operators
* [Functions](#function-list)

For more information, you can refer to:

* [Microsoft Formula documentation](https://support.microsoft.com/en-us/office/overview-of-formulas-in-excel-ecfdc708-9162-49e8-b993-c311f47ca173)
* [Microsoft Operator documentation](https://support.microsoft.com/en-gb/office/calculation-operators-and-precedence-in-excel-48be406d-4975-4d31-b2b8-7af9e0e2878a)

To use `=` as a string at the beginning of a cell, start with an apostrophe: `'=`.


## Function List

The Sunfish Spreadsheet supports a large variety of functions. They work in the same way as in other Excel editors, so the following documentations are also applicable:

* [Microsoft Excel functions](https://support.microsoft.com/en-us/office/excel-functions-alphabetical-b3944572-255d-4efb-bb96-c6d90033e188)
* [Google Sheets functions](https://support.google.com/docs/table/25273)

The function names are case insensitive.

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

<table>
<thead><tr><th>Function Name</th><th>Description</th></tr></thead>
<tbody>
<tr><td><code>ABS</code></td><td>Returns the absolute (non-negative) value of a number.</td></tr>
<tr><td><code>ACOS</code></td><td>Returns the principal value of the arccosine of a number in radians.</td></tr>
<tr><td><code>ACOSH</code></td><td>Returns the principal value of the inverse hyperbolic cosine of a number.</td></tr>
<tr><td><code>ACOT</code></td><td>Returns the principal value of the arccotangent of a number in radians.</td></tr>
<tr><td><code>ACOTH</code></td><td>Returns the hyperbolic arccotangent of a number.</td></tr>
<tr><td><code>ADDRESS</code></td><td>Returns a cell address (reference) as text.</td></tr>
<tr><td><code>AGGREGATE</code></td><td>Returns an aggregate of a list or database.</td></tr>
<tr><td><code>ARABIC</code></td><td>Converts Roman numbers to Arabic as numbers.</td></tr>
<tr><td><code>AREAS</code></td><td>Returns the number of areas in a reference.</td></tr>
<tr><td><code>ASIN</code></td><td>Returns the principal value of the arcsine of a number in radians.</td></tr>
<tr><td><code>ASINH</code></td><td>Returns the principal value of the inverse hyperbolic sine of a number.</td></tr>
<tr><td><code>ATAN</code></td><td>Returns the principal value of the arctangent of a number in radians.</td></tr>
<tr><td><code>ATAN2</code></td><td>Returns the principal value of the arctangent from x- and y- coordinates in radians.</td></tr>
<tr><td><code>ATANH</code></td><td>Returns the principal value of the inverse hyperbolic tangent of a number.</td></tr>
<tr><td><code>AVEDEV</code></td><td>Calculates the average of the absolute deviations of listed values.</td></tr>
<tr><td><code>AVERAGE</code></td><td>Returns the average of a set of numbers.</td></tr>
<tr><td><code>AVERAGEA</code></td><td>Returns the average of values, including numbers, text, and logical values.</td></tr>
<tr><td><code>AVERAGEIF</code></td><td>Returns the average of all cells in a range based on a given criteria.</td></tr>
<tr><td><code>AVERAGEIFS</code></td><td>Returns the average of all cells in a range based on multiple criteria.</td></tr>
<tr><td><code>BASE</code></td><td>Converts a number into a text representation with the given base.</td></tr>
<tr><td><code>BETA.DIST</code></td><td>Returns the beta cumulative distribution function.</td></tr>
<tr><td><code>BETA.INV</code></td><td>Returns the inverse of the cumulative distribution function for a specified beta distribution.</td></tr>
<tr><td><code>BETADIST</code></td><td>Returns the value of the probability density function or the cumulative distribution function for the beta distribution.</td></tr>
<tr><td><code>BINOM.DIST</code></td><td>Returns the individual term binomial distribution probability.</td></tr>
<tr><td><code>BINOM.DIST.RANGE</code></td><td>Returns the probability of a trial result using a binomial distribution.</td></tr>
<tr><td><code>BINOM.INV</code></td><td>Returns the smallest value for which the cumulative binomial distribution is less than or equal to a criterion value.</td></tr>
<tr><td><code>BINOMDIST</code></td><td>Returns the binomial distribution probability.</td></tr>
<tr><td><code>CEILING</code></td><td>Rounds a number to the nearest integer or to the nearest multiple of significance.</td></tr>
<tr><td><code>CEILING.MATH</code></td><td>Rounds a number up, to the nearest integer or to the nearest multiple of significance.</td></tr>
<tr><td><code>CEILING.PRECISE</code></td><td>Rounds a number to the nearest integer or to the nearest multiple of significance. Regardless of the sign of the number, the number is rounded up.</td></tr>
<tr><td><code>CHAR</code></td><td>Returns character represented by a given number.</td></tr>
<tr><td><code>CHISQ.DIST</code></td><td>Returns the cumulative beta probability density function.</td></tr>
<tr><td><code>CHISQ.DIST.RT</code></td><td>Returns the one-tailed probability of the chi-squared distribution.</td></tr>
<tr><td><code>CHISQ.INV</code></td><td>Returns the cumulative beta probability density function.</td></tr>
<tr><td><code>CHISQ.INV.RT</code></td><td>Returns the inverse of the one-tailed probability of the chi-squared distribution.</td></tr>
<tr><td><code>CHISQ.TEST</code></td><td>Returns the test for independence.</td></tr>
<tr><td><code>CHOOSE</code></td><td>Uses an index to return a value from a list of values.</td></tr>
<tr><td><code>CLEAN</code></td><td>Removes all nonprintable characters from a text.</td></tr>
<tr><td><code>CODE</code></td><td>Returns a numeric value corresponding to the first character in a text string.</td></tr>
<tr><td><code>COLUMN</code></td><td>Returns the column number(s) of a reference.</td></tr>
<tr><td><code>COLUMNS</code></td><td>Returns the number of columns in a given range.</td></tr>
<tr><td><code>COMBIN</code></td><td>Returns the number of combinations for a given number of objects.</td></tr>
<tr><td><code>COMBINA</code></td><td>Returns the number of combinations with repetitions for a given number of objects.</td></tr>
<tr><td><code>CONCATENATE</code></td><td>Joins a number of text strings into one text string.</td></tr>
<tr><td><code>CONFIDENCE.NORM</code></td><td>Returns the confidence interval for a population mean.</td></tr>
<tr><td><code>CONFIDENCE.T</code></td><td>Returns the confidence interval for a population mean, using a Student's t distribution.</td></tr>
<tr><td><code>COS</code></td><td>Returns the cosine of a number. The angle is returned in radians.</td></tr>
<tr><td><code>COSH</code></td><td>Returns the hyperbolic cosine of a number.</td></tr>
<tr><td><code>COT</code></td><td>Returns the cotangent of an angle, specified in radians.</td></tr>
<tr><td><code>COTH</code></td><td>Returns the hyperbolic cotangent of a number.</td></tr>
<tr><td><code>COUNT</code></td><td>Counts the number of numbers in a list of arguments.</td></tr>
<tr><td><code>COUNTA</code></td><td>Counts the number of values in a list of arguments.</td></tr>
<tr><td><code>COUNTBLANK</code></td><td>Counts the number of blank cells in a range.</td></tr>
<tr><td><code>COUNTIF</code></td><td>Counts the number of cells in a range that meet a criteria.</td></tr>
<tr><td><code>COUNTIFS</code></td><td>Counts the number of cells in a range that meet multiple criteria.</td></tr>
<tr><td><code>COVAR</code></td><td>Calculates the covariance between two cell ranges.</td></tr>
<tr><td><code>COVARIANCE.P</code></td><td>Returns covariance, the average of the products of paired deviations.</td></tr>
<tr><td><code>COVARIANCE.S</code></td><td>Returns the sample covariance, the average of the product deviations for each data point pair in two data sets.</td></tr>
<tr><td><code>CRITBINOM</code></td><td>Returns the smallest value for which the cumulative binomial distribution is less than or equal to a criterion value.</td></tr>
<tr><td><code>CSC</code></td><td>Returns the cosecant of an angle, specified in radians.</td></tr>
<tr><td><code>CSCH</code></td><td>Returns the hyperbolic cosecant of an angle, specified in radians.</td></tr>
<tr><td><code>DATE</code></td><td>Returns a date value constructed from year, month, and day values.</td></tr>
<tr><td><code>DATEVALUE</code></td><td>Returns the date converting it into the form of text to a serial number.</td></tr>
<tr><td><code>DAY</code></td><td>Returns the day by converting it from a serial number.</td></tr>
<tr><td><code>DAYS</code></td><td>Returns the number of days between two dates.</td></tr>
<tr><td><code>DAYS360</code></td><td>Returns the number of days between two dates using the 360-day year.</td></tr>
<tr><td><code>DECIMAL</code></td><td>Converts a text representation of a number in a given base into a decimal number.</td></tr>
<tr><td><code>DEGREES</code></td><td>Converts radians to degrees.</td></tr>
<tr><td><code>DOLLAR</code></td><td>Converts a number to text, using the <code>$</code> currency format.</td></tr>
<tr><td><code>EDATE</code></td><td>Returns the serial number of the date that is the indicated number of months before or after the start date.</td></tr>
<tr><td><code>EOMONTH</code></td><td>Returns the serial number of the last day of the month before or after a specified number of months.</td></tr>
<tr><td><code>ERF</code></td><td>Returns the error function.</td></tr>
<tr><td><code>ERFC</code></td><td>Returns the complementary error function.</td></tr>
<tr><td><code>EVEN</code></td><td>Rounds a number up to the nearest even integer.</td></tr>
<tr><td><code>EXACT</code></td><td>Reports if two text values are equal using a case-sensitive comparison.</td></tr>
<tr><td><code>EXP</code></td><td>Returns <code>e</code> raised to the power of a given number.</td></tr>
<tr><td><code>EXPON.DIST</code></td><td>Returns the exponential distribution.</td></tr>
<tr><td><code>F.DIST</code></td><td>Returns the <code>F</code> probability distribution.</td></tr>
<tr><td><code>F.DIST.RT</code></td><td>Returns the <code>F</code> probability distribution.</td></tr>
<tr><td><code>F.INV</code></td><td>Returns the inverse of the <code>F</code> probability distribution.</td></tr>
<tr><td><code>F.INV.RT</code></td><td>Returns the inverse of the <code>F</code> probability distribution.</td></tr>
<tr><td><code>F.TEST</code></td><td>Returns the result of an <code>F</code>-test.</td></tr>
<tr><td><code>FACT</code></td><td>Returns the factorial of a number.</td></tr>
<tr><td><code>FACTDOUBLE</code></td><td>Returns the double factorial of a number.</td></tr>
<tr><td><code>FALSE</code></td><td>Returns logical value <code>false</code>.</td></tr>
<tr><td><code>FIND</code></td><td>Returns the starting position of a given text.</td></tr>
<tr><td><code>FISHER</code></td><td>Returns the Fisher transformation.</td></tr>
<tr><td><code>FISHERINV</code></td><td>Returns the inverse of the Fisher transformation.</td></tr>
<tr><td><code>FIXED</code></td><td>Rounds the number to a specified number of decimals and formats the result as text.</td></tr>
<tr><td><code>FLOOR</code></td><td>Rounds a number down to the nearest multiple of the second parameter.</td></tr>
<tr><td><code>FLOOR.MATH</code></td><td>Rounds a number down, to the nearest integer or to the nearest multiple of significance.</td></tr>
<tr><td><code>FLOOR.PRECISE</code></td><td>Rounds a number down to the nearest integer or to the nearest multiple of significance. Regardless of the sign of the number, the number is rounded down.</td></tr>
<tr><td><code>FORECAST</code></td><td>Assumes a future value based on existing x- and y- values.</td></tr>
<tr><td><code>FORMULATEXT</code></td><td>Returns the formula at the given reference as text.</td></tr>
<tr><td><code>FREQUENCY</code></td><td>Returns a frequency distribution as a vertical array.</td></tr>
<tr><td><code>GAMMA</code></td><td>Returns Gamma function value.</td></tr>
<tr><td><code>GAMMA.DIST</code></td><td>Returns the Gamma distribution.</td></tr>
<tr><td><code>GAMMA.INV</code></td><td>Returns the inverse of the Gamma cumulative distribution.</td></tr>
<tr><td><code>GAMMALN</code></td><td>Returns the natural logarithm of the Gamma function.</td></tr>
<tr><td><code>GAUSS</code></td><td>Returns 0.5 less than the standard normal cumulative distribution.</td></tr>
<tr><td><code>GCD</code></td><td>Returns the greatest common divisor (GCD).</td></tr>
<tr><td><code>GEOMEAN</code></td><td>Returns the geometric mean of a sequence.</td></tr>
<tr><td><code>HARMEAN</code></td><td>Returns the harmonic mean of a sequence.</td></tr>
<tr><td><code>HLOOKUP</code></td><td>Looks for a matching value in the first row of a given table and returns the value of the indicated row.</td></tr>
<tr><td><code>HOUR</code></td><td>Converts a serial number to an hour.</td></tr>
</tbody>
</table>
| `HYPERLINK `| Creates a hyperlink involving an evaluated expression. |

<table>
<thead><tr><th>Function Name</th><th>Description</th></tr></thead>
<tbody>
<tr><td><code>IF</code></td><td>Returns one of two values, depending on a condition.</td></tr>
<tr><td><code>IFERROR</code></td><td>Returns a specified value if a formula evaluates to an error; otherwise, returns the result of the formula.</td></tr>
<tr><td><code>INDEX</code></td><td>Returns a value or a reference to a value from within a table or range.</td></tr>
<tr><td><code>INDIRECT</code></td><td>Returns a reference indicated by a text value.</td></tr>
<tr><td><code>INT</code></td><td>Rounds a number down to the nearest integer.</td></tr>
<tr><td><code>INTERCEPT</code></td><td>Returns the intercept of the linear regression line for the given data.</td></tr>
<tr><td><code>ISBLANK</code></td><td>Returns <code>true</code> if the referenced cell is blank; else returns <code>false</code>.</td></tr>
<tr><td><code>ISERR</code></td><td>Returns <code>true</code> if the value is any error except <code>#N/A</code>; else returns <code>false</code>.</td></tr>
<tr><td><code>ISERROR</code></td><td>Returns <code>true</code> if the value is any error; else returns <code>false</code>.</td></tr>
<tr><td><code>ISEVEN</code></td><td>Returns <code>true</code> if the value is even; else returns <code>false</code>.</td></tr>
<tr><td><code>ISLOGICAL</code></td><td>Returns <code>true</code> if the value is logical; else returns <code>false</code>.</td></tr>
<tr><td><code>ISNA</code></td><td>Returns <code>true</code> if the value is the <code>#N/A</code> error; else returns <code>false</code>.</td></tr>
<tr><td><code>ISNONTEXT</code></td><td>Returns <code>true</code> if the value is not text; else returns <code>false</code>.</td></tr>
<tr><td><code>ISNUMBER</code></td><td>Returns <code>true</code> if the value is a number; else returns <code>false</code>.</td></tr>
<tr><td><code>ISO.CEILING</code></td><td>Returns a number that is rounded up to the nearest integer or to the nearest multiple of significance.</td></tr>
<tr><td><code>ISODD</code></td><td>Returns <code>true</code> if the value is odd; else returns <code>false</code>.</td></tr>
<tr><td><code>ISOWEEKNUM</code></td><td>Returns the ISO week number of the year for a given date.</td></tr>
<tr><td><code>ISREF</code></td><td>Returns <code>true</code> if the value is a reference; else returns <code>false</code>.</td></tr>
<tr><td><code>ISTEXT</code></td><td>Returns <code>true</code> if the value is text; else returns <code>false</code>.</td></tr>
<tr><td><code>KURT</code></td><td>Returns the kurtosis (“peakedness”) of a data set.</td></tr>
<tr><td><code>LARGE</code></td><td>Finds the nth largest value in a list.</td></tr>
<tr><td><code>LCM</code></td><td>Returns the least common multiple.</td></tr>
<tr><td><code>LEFT</code></td><td>Returns a selected number of text characters from the left.</td></tr>
<tr><td><code>LEN</code></td><td>Returns the number of characters from a given text.</td></tr>
<tr><td><code>LINEST</code></td><td>Returns the parameters of a (simple or multiple) linear regression equation for the given data and, optionally, statistics on this regression.</td></tr>
<tr><td><code>LN</code></td><td>Returns the natural logarithm of a number.</td></tr>
<tr><td><code>LOG</code></td><td>Returns the logarithm of a number to a specified base.</td></tr>
<tr><td><code>LOG10</code></td><td>Returns the base-10 logarithm of a number.</td></tr>
<tr><td><code>LOGEST</code></td><td>Returns the parameters of an exponential regression equation for the given data obtained by linearizing this intrinsically linear response function and returns, optionally, statistics on this regression.</td></tr>
<tr><td><code>LOGNORM.DIST</code></td><td>Returns the cumulative lognormal distribution.</td></tr>
<tr><td><code>LOGNORM.INV</code></td><td>Returns the inverse of the lognormal cumulative distribution.</td></tr>
<tr><td><code>LOWER</code></td><td>Converts text to lowercase.</td></tr>
<tr><td><code>MATCH</code></td><td>Finds an item in a range of cells and returns its relative position (starting from 1).</td></tr>
<tr><td><code>MAX</code></td><td>Returns the maximum value in a set of numbers.</td></tr>
<tr><td><code>MDETERM</code></td><td>Returns the determinant of a matrix.</td></tr>
<tr><td><code>MEDIAN</code></td><td>Returns the median (middle) value in a list of numbers.</td></tr>
<tr><td><code>MID</code></td><td>Returns a specific number of characters from a text string, starting at a specified position.</td></tr>
<tr><td><code>MIN</code></td><td>Returns the minimum value in a set of numbers.</td></tr>
<tr><td><code>MINUTE</code></td><td>Converts a serial number into a minute.</td></tr>
<tr><td><code>MINVERSE</code></td><td>Returns the inverse of a matrix.</td></tr>
<tr><td><code>MMULT</code></td><td>Returns the matrix output of two arrays.</td></tr>
<tr><td><code>MOD</code></td><td>Returns the remainder when one number is divided by another number.</td></tr>
<tr><td><code>MODE.MULT</code></td><td>Returns a vertical array of the most frequently occurring, or repetitive values in an array or range of data.</td></tr>
<tr><td><code>MODE.SNGL</code></td><td>Returns the most common value in a data set.</td></tr>
<tr><td><code>MONTH</code></td><td>Converts a serial number to a month.</td></tr>
<tr><td><code>MROUND</code></td><td>Rounds the number to the desired multiple.</td></tr>
<tr><td><code>MULTINOMIAL</code></td><td>Returns the multinomial for a given set of values.</td></tr>
<tr><td><code>MUNIT</code></td><td>Creates a unit matrix of a specified dimension.</td></tr>
<tr><td><code>N</code></td><td>Returns the number of a value.</td></tr>
<tr><td><code>NA</code></td><td>Returns the error value <code>#N/A</code>.</td></tr>
<tr><td><code>NEGBINOM.DIST</code></td><td>Returns the negative binomial distribution.</td></tr>
<tr><td><code>NEGBINOMDIST</code></td><td>Returns the negative binomial distribution.</td></tr>
<tr><td><code>NETWORKDAYS</code></td><td>Returns the number of whole workdays between two dates.</td></tr>
<tr><td><code>NORM.DIST</code></td><td>Returns the normal cumulative distribution.</td></tr>
<tr><td><code>NORM.INV</code></td><td>Returns the inverse of the normal cumulative distribution.</td></tr>
<tr><td><code>NORM.S.DIST</code></td><td>Returns the standard normal cumulative distribution.</td></tr>
<tr><td><code>NORM.S.INV</code></td><td>Returns the inverse of the standard normal cumulative distribution.</td></tr>
<tr><td><code>NOT</code></td><td>Reverses the logic of its argument.</td></tr>
<tr><td><code>NOW</code></td><td>Returns the serial number of the current date and time.</td></tr>
<tr><td><code>ODD</code></td><td>Rounds a number up to the nearest odd integer, where "up" means "away from 0".</td></tr>
<tr><td><code>OFFSET</code></td><td>Modifies the position and dimension of a reference.</td></tr>
<tr><td><code>PEARSON</code></td><td>Returns the Pearson correlation coefficient of two data sets.</td></tr>
<tr><td><code>PERCENTILE</code></td><td>Calculates the x-th sample percentile of values in a range.</td></tr>
<tr><td><code>PERCENTILE.EXC</code></td><td>`Returns the k-th percentile of values in a range, where k is in the range 0..1, exclusive.</td></tr>
<tr><td><code>PERCENTILE.INC</code></td><td>`Returns the k-th percentile of values in a range.</td></tr>
<tr><td><code>PERCENTRANK</code></td><td>Returns the percentage rank of a value in a sample.</td></tr>
<tr><td><code>PERCENTRANK.EXC</code></td><td>`Returns the rank of a value in a data set as a percentage (0..1, exclusive) of the data set.</td></tr>
<tr><td><code>PERCENTRANK.INC</code></td><td>`Returns the percentage rank of a value in a data set.</td></tr>
<tr><td><code>PHI</code></td><td>Returns the value of the density function for a standard normal distribution.</td></tr>
<tr><td><code>PI</code></td><td>Returns the approximate value of pi.</td></tr>
<tr><td><code>POISSON.DIST</code></td><td>Returns the Poisson distribution.</td></tr>
<tr><td><code>POWER</code></td><td>Returns the result of a number raised to the power of another number.</td></tr>
<tr><td><code>PROB</code></td><td>Returns the probability that values in a range are between two limits.</td></tr>
<tr><td><code>PRODUCT</code></td><td>Multiplies the set of numbers, including all numbers inside ranges.</td></tr>
<tr><td><code>PROPER</code></td><td>Capitalizes the first letter in each word of a text value.</td></tr>
<tr><td><code>QUARTILE</code></td><td>Returns the quartile of a data set.</td></tr>
<tr><td><code>QUARTILE.EXC</code></td><td>Returns the quartile of the data set, based on percentile values from 0..1, exclusive.</td></tr>
<tr><td><code>QUARTILE.INC</code></td><td>Returns the quartile of a data set.</td></tr>
<tr><td><code>QUOTIENT</code></td><td>Returns the integer portion of a division.</td></tr>
</tbody>
</table>

<table>
<thead><tr><th>Function Name</th><th>Description</th></tr></thead>
<tbody>
<tr><td><code>RADIANS</code></td><td>Converts degrees to radians.</td></tr>
<tr><td><code>RAND</code></td><td>Returns a random number between 0 (inclusive) and 1 (exclusive).</td></tr>
<tr><td><code>RANDBETWEEN</code></td><td>Returns a random number between specified values.</td></tr>
<tr><td><code>RANK</code></td><td>Returns the rank of a number in a list of numbers.</td></tr>
<tr><td><code>RANK.AVG</code></td><td>Returns the rank of a number in a list of numbers.</td></tr>
<tr><td><code>RANK.EQ</code></td><td>Returns the rank of a number in a list of numbers.</td></tr>
<tr><td><code>REPLACE</code></td><td>Replaces characters within text.</td></tr>
<tr><td><code>REPT</code></td><td>Repeats text a specified number of times.</td></tr>
<tr><td><code>RIGHT</code></td><td>Returns the rightmost characters from a text value.</td></tr>
<tr><td><code>ROMAN</code></td><td>Converts Arabic numbers to Roman as text.</td></tr>
<tr><td><code>ROUNDDOWN</code></td><td>Rounds a number down, towards zero, to the number of digits specified by <code>digits</code>.</td></tr>
<tr><td><code>ROUNDUP</code></td><td>Rounds a number up, away from 0 (zero), to the number of digits specified by <code>digits</code>.</td></tr>
<tr><td><code>ROW</code></td><td>Returns the row number(s) of a reference.</td></tr>
<tr><td><code>ROWS</code></td><td>Returns the number of rows in a reference.</td></tr>
<tr><td><code>RSQ</code></td><td>Returns the square of the Pearson product moment correlation coefficient.</td></tr>
<tr><td><code>SEARCH</code></td><td>Finds a text value within another text value (not case-sensitive).</td></tr>
<tr><td><code>SEC</code></td><td>Returns the secant of an angle specified in radians.</td></tr>
<tr><td><code>SECH</code></td><td>Returns the hyperbolic secant of a given angle specified in radians.</td></tr>
<tr><td><code>SECOND</code></td><td>Converts a serial number to a second. This function presumes that leap seconds never exist.</td></tr>
<tr><td><code>SERIESSUM</code></td><td>Returns the sum of a power series based on the formula.</td></tr>
<tr><td><code>SIGN</code></td><td>Returns the sign of a number.</td></tr>
<tr><td><code>SIN</code></td><td>Returns the sine of an angle specified in radians.</td></tr>
<tr><td><code>SINH</code></td><td>Returns the hyperbolic sine of a number.</td></tr>
<tr><td><code>SLOPE</code></td><td>Calculates the slope of the linear regression line.</td></tr>
<tr><td><code>SMALL</code></td><td>Finds the n-th smallest value in a data set.</td></tr>
<tr><td><code>SQRT</code></td><td>Returns a positive square root of a number.</td></tr>
<tr><td><code>SQRTPI</code></td><td>Returns the square root of a number multiplied by pi.</td></tr>
<tr><td><code>STDEV.P</code></td><td>Calculates the standard deviation based on the entire population.</td></tr>
<tr><td><code>STDEV.S</code></td><td>Estimates the standard deviation based on a sample.</td></tr>
<tr><td><code>STEYX</code></td><td>Returns the standard error of the predicted y-value for each x in the regression.</td></tr>
<tr><td><code>SUBSTITUTE</code></td><td>Substitutes new text for old text string.</td></tr>
<tr><td><code>SUBTOTAL</code></td><td>Evaluates a function on a range.</td></tr>
<tr><td><code>SUM</code></td><td>Sums (adds) the set of numbers, including all numbers in a range.</td></tr>
<tr><td><code>SUMIF</code></td><td>Sums the values of cells in a range that meets a given criterion.</td></tr>
<tr><td><code>SUMIFS</code></td><td>Sums the values of cells in a range that meets multiple criteria.</td></tr>
<tr><td><code>SUMPRODUCT</code></td><td>Returns the sum of the products of corresponding array elements.</td></tr>
<tr><td><code>SUMSQ</code></td><td>Sums (adds) the set of squares of numbers, including all numbers in a range.</td></tr>
<tr><td><code>SUMX2MY2</code></td><td>Returns the sum of the difference between the squares of corresponding values in two arrays.</td></tr>
<tr><td><code>SUMX2PY2</code></td><td>Returns the sum of squares of corresponding values in two arrays.</td></tr>
<tr><td><code>SUMXMY2</code></td><td>Returns the sum of squares of corresponding values in two arrays.</td></tr>
<tr><td><code>T</code></td><td>Converts its arguments to text; else returns a zero-length text value.</td></tr>
<tr><td><code>T.DIST</code></td><td>Returns the Percentage Points (probability) for the Student t-distribution.</td></tr>
<tr><td><code>T.DIST.2T</code></td><td>Returns the Percentage Points (probability) for the Student t-distribution.</td></tr>
<tr><td><code>T.DIST.RT</code></td><td>Returns the Student's t-distribution.</td></tr>
<tr><td><code>T.INV</code></td><td>Returns the t-value of the Student's t-distribution as a function of the probability and the degrees of freedom.</td></tr>
<tr><td><code>T.INV.2T</code></td><td>Returns the inverse of the Student's t-distribution.</td></tr>
<tr><td><code>T.TEST</code></td><td>Returns the probability associated with a Student's t-test.</td></tr>
<tr><td><code>TAN</code></td><td>Returns the tangent of a number in radians.</td></tr>
<tr><td><code>TANH</code></td><td>Returns the hyperbolic tangent of a number.</td></tr>
<tr><td><code>TEXT</code></td><td>Formats a number and converts it to text.</td></tr>
<tr><td><code>TIME</code></td><td>Constructs a time value from hours, minutes, and seconds.</td></tr>
<tr><td><code>TIMEVALUE</code></td><td>Returns the serial number of a particular time.</td></tr>
<tr><td><code>TODAY</code></td><td>Returns the serial number of today's date.</td></tr>
<tr><td><code>TRANSPOSE</code></td><td>Returns the transpose of an array.</td></tr>
<tr><td><code>TRIM</code></td><td>Removes spaces from text; replaces all internal multiple spaces with a single space.</td></tr>
<tr><td><code>TRIMMEAN</code></td><td>Returns the mean of the interior of a data set, ignoring a proportion of high and low values.</td></tr>
<tr><td><code>TRUE</code></td><td>Returns the logical value <code>true</code>.</td></tr>
<tr><td><code>UNICHAR</code></td><td>Returns the character represented by the given numeric value according to the [Unicode Standard](https://unicode.org/standard/standard.html).</td></tr>
<tr><td><code>UNICODE</code></td><td>Returns the [Unicode](https://unicode.org/standard/standard.html) code point that corresponds to the first character of a text value.</td></tr>
<tr><td><code>UPPER</code></td><td>Converts text to uppercase.</td></tr>
<tr><td><code>VALUE</code></td><td>Converts a text argument to a number.</td></tr>
<tr><td><code>VAR.P</code></td><td>Calculates variance based on the entire population.</td></tr>
<tr><td><code>VAR.S</code></td><td>Estimates variance based on a sample.</td></tr>
<tr><td><code>VLOOKUP</code></td><td>Looks for a matching value in a table or a range by row.</td></tr>
<tr><td><code>WEEKDAY</code></td><td>Converts a serial number to a day of the week.</td></tr>
<tr><td><code>WEEKNUM</code></td><td>Determines the week number of the year for a given date.</td></tr>
<tr><td><code>WORKDAY</code></td><td>Returns the date serial number which is a specified number of work days before or after an input date.</td></tr>
<tr><td><code>YEAR</code></td><td>Converts a serial number to a year.</td></tr>
<tr><td><code>YEARFRAC</code></td><td>Extracts the number of years (including the fractional part) between two dates.</td></tr>
</tbody>
</table>


## See Also

* [Live Demo: Spreadsheet Events](https://demos.sunfish.dev/blazor-ui/spreadsheet/events)
