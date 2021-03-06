/*
 *  THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
 *  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 *  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
 *  PURPOSE. IT CAN BE DISTRIBUTED FREE OF CHARGE AS LONG AS THIS HEADER 
 *  REMAINS UNCHANGED.
 *
 *  REPO: http://www.github.com/tomwilsoncoder/RTS
*/
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public unsafe partial class Hotloader {
    private void load(HotloaderFile file, byte* data, int length) {
        byte* ptr = data;
        byte* ptrEnd = data + length;

        byte* blockStart = ptr;
        byte* blockEnd = ptr;

        bool negativeFlag = false;

        //keep track of what classes/variables are being added.
        List<HotloaderVariable> fileVariables = new List<HotloaderVariable>();
        List<HotloaderFile> fileIncludes = new List<HotloaderFile>();

        //
        HotloaderClass currentClass = p_GlobalClass;
        HotloaderVariable currentVariable = null;
        HotloaderExpression currentExpression = null;

        //define what mode we are in (what type of block
        //we are reading).
        parserMode mode = parserMode.NONE;

        //
        HotloaderAccessor currentAccessor = HotloaderAccessor.NONE;

        //where we are in a more usable way.
        int currentLine = 1;
        int currentColumn = 0;

        while (ptr != ptrEnd) {
            byte current = *(ptr++);
            currentColumn++;

            //are we at the end of the file?
            bool atEnd = ptr == ptrEnd;

            #region control characters
            //newline?
            bool newLine =
                current == '\n' ||
                current == '\r';
            if (newLine) {
                if (current == '\n') {
                    currentColumn = 0;
                    currentLine++;
                }
            }

            //whitespace
            bool whitespace =
                newLine ||
                current == ' ' ||
                current == '\t';

            //alpha numeric? 
            //(only make the call if
            //we know the character is not a whitespace.
            bool nameChar =
                !whitespace &&
                isNameCharacter(current);
            #endregion
                        
            #region byte block evaluation
            bool evaluateBlock = atEnd || whitespace || !nameChar;

            if (evaluateBlock) {
                //if we are at the end, make sure
                //we include the last character
                //in the block.
                if (atEnd && !whitespace && nameChar) { blockEnd++; }
                
                //is the block blank?
                bool isBlank = blockStart == blockEnd;
                int blockLength = (int)(blockEnd - blockStart);

                //read the block as a string
                if (!isBlank) {
                    handleBlock(
                        fileVariables,
                        file,
                        blockStart,
                        blockEnd,
                        currentLine,
                        currentColumn - blockLength + 1,
                        ref negativeFlag,
                        ref mode,
                        ref currentAccessor,
                        ref currentVariable,
                        ref currentExpression,
                        ref currentClass);

                    //block been changed outside pointer?
                    if (blockStart > ptr) {
                        ptr = blockStart;
                    }
                }

                //reset
                blockStart = blockEnd = ptr;
                
                //do not process anything else if it is a whitespace
                if (whitespace) {
                    continue;
                }
            }
            #endregion

            #region comment
            if (current == '#') { 
                //skip over line
                while (ptr != ptrEnd && *(ptr++) != '\n') { }
                
                //we hit newline?
                if (*(ptr - 1) == '\n') { ptr--; }
                blockStart = blockEnd = ptr;
                continue;
            }
            #endregion

            #region string literal
            if (current == '"') {
                //valid?
                if (mode != parserMode.ASSIGNMENT &&
                    mode != parserMode.INCLUDE) {
                    throw new HotloaderParserException(
                        file,
                        currentLine,
                        currentColumn,
                        "Unexpected string literal");
                }

                byte* literalStart = ptr;
                if (!readStringLiteral(ref ptr, ptrEnd)) {
                    throw new HotloaderParserException(
                        file,
                        currentLine,
                        currentColumn,
                        "String literal did not terminate");
                }

                //deturmine where the string ends
                byte* literalEnd = ptr - 1;

                //read the string and remove 
                //string terminating characters
                string read = readString(literalStart, literalEnd);
                read = read.Replace("\\", "");

                //include?
                if (mode == parserMode.INCLUDE) {                    
                    mode = parserMode.NONE;
                    if (!File.Exists(read)) {
                        throw new HotloaderParserException(
                            file,
                            currentLine,
                            currentColumn,
                            String.Format(
                                "Cannot include file \"{0}\". Does not exist.",
                                read));
                    }

                    //does the file exist?
                    HotloaderFile include = GetFile(read);
                    if (include == null) {
                        include = AddFile(read);
                        fileIncludes.Add(include);
                    }
                    else if (file.Includes.Contains(include)) {
                        fileIncludes.Add(include);
                    }

                }
                else {
                    //add operand
                    currentExpression.AddOperand(
                        read,
                        HotloaderValueType.STRING,
                        currentLine,
                        currentColumn,
                        file);
                }

                //update line position
                currentColumn += (int)(literalEnd - literalStart) + 1;

                blockStart = blockEnd = ptr;
                continue;
            }
            #endregion

            #region expression scopes

            if (current == '(' || current == ')') { 
                //valid?
                if (mode != parserMode.ASSIGNMENT) {
                    throw new HotloaderParserException(
                        file,
                        currentLine,
                        currentColumn,
                        "Unexpected expression character");
                }

                //close?
                if (current == ')') { 
                    //can we close?
                    if (currentExpression.Parent == null) {
                        throw new HotloaderParserException(
                            file,
                            currentLine,
                            currentColumn,
                            "Unexpected end of expression scope");
                    }

                    //add the expression as an operand
                    HotloaderExpression parent = currentExpression.Parent;
                    HotloaderExpression expression = currentExpression;
                    HotloaderValueOperand op = parent.AddOperand(
                        expression, 
                        HotloaderValueType.EVALUATION, 
                        currentLine, 
                        currentColumn,
                        file);

                    //close
                    currentExpression = currentExpression.Parent;
                }

                //open?
                if (current == '(') {
                    HotloaderExpression newExpression = new HotloaderExpression(this, currentVariable);
                    newExpression.Parent = currentExpression;
                    currentExpression = newExpression;
                }

                //
                blockStart = blockEnd = ptr;
                continue;
            }

            #endregion

            #region operators
            #region class
            if (current == ':') { 
                //valid?
                if (mode != parserMode.NONE) {
                    throw new HotloaderParserException(
                        file,
                        currentLine,
                        currentColumn,
                        "Unexpected class symbol");
                }

                mode = parserMode.CLASS;
                blockStart = blockEnd = ptr;
                continue;
            }
            #endregion

            #region assignment
            if (current == '=') { 
                //valid?
                if (mode != parserMode.VARIABLE) {
                    throw new HotloaderParserException(
                            file,
                        currentLine,
                        currentColumn,
                        "Unexpected assignment operator");
                }
                mode = parserMode.ASSIGNMENT;
                blockStart = blockEnd = ptr;
                continue;
            }
            #endregion

            #region end assignment
            if (current == ';') { 
                //valid?
                if (mode != parserMode.ASSIGNMENT ||
                    !currentExpression.Valid ||
                    currentExpression.Parent != null ||
                    currentExpression.Empty) {
                        throw new HotloaderParserException(
                                file,
                        currentLine,
                        currentColumn,
                        "Unexpected end-of-expression character");
                }

                mode = parserMode.NONE;
                blockStart = blockEnd = ptr;

                //poll the expression to mark the end of 
                //an assignment so it can do necassary 
                //functions (e.g invoke assignment callback)
                currentExpression.Poll();

                currentExpression = null;
                currentVariable = null;
                continue;
            }
            #endregion

            #region value operators
            HotloaderValueOperator valueOp = HotloaderValueOperator.NONE;
            switch ((char)current) {
                case '+': valueOp = HotloaderValueOperator.ADD; break;
                case '-': valueOp = HotloaderValueOperator.SUBTRACT; break;
                case '*': valueOp = HotloaderValueOperator.MULTIPLY; break;
                case '/': valueOp = HotloaderValueOperator.DIVIDE; break;
                case '^': valueOp = HotloaderValueOperator.POWER; break;
                case '%': valueOp = HotloaderValueOperator.MODULUS; break;
               
                case '&': valueOp = HotloaderValueOperator.AND; break;
                case '|': valueOp = HotloaderValueOperator.OR; break;
                case '?': valueOp = HotloaderValueOperator.XOR; break;
                case '<': valueOp = HotloaderValueOperator.SHIFTL; break;
                case '>': valueOp = HotloaderValueOperator.SHIFTR; break;

                case '!': valueOp = HotloaderValueOperator.NOT; break;
            }

            //are we expecting a math operation?
            if (valueOp != HotloaderValueOperator.NONE &&
                mode != parserMode.ASSIGNMENT) {
                   throw new HotloaderParserException(
                       file,
                       currentLine,
                       currentColumn,
                       String.Format(
                            "Unexpected operator {0}",
                            (char)current));
            }

            //wait, was this negative?
            bool addOp = true;
            if (valueOp == HotloaderValueOperator.SUBTRACT) { 
                //make sure there would be an operand before
                //the subtract. Otherwise we assume it's a 
                //negative integer/decimal.
                negativeFlag =
                    currentExpression.Operands ==
                    currentExpression.Operators;
                //addOp = false;
            }

            if (valueOp != HotloaderValueOperator.NONE) {
                //if we have discovered a negate operator
                //do not add this as a maths operator!
                if (addOp) {
                    currentExpression.AddOperator(valueOp);                
                }

                blockStart = blockEnd = ptr;
                continue;
            }
            #endregion
            #endregion

            //invalid character?
            if (!nameChar) {
                throw new HotloaderParserException(
                    file,
                    currentLine,
                    currentColumn,
                    String.Format(
                        "Invalid character {0}",
                        (char)current));
            }

            //incriment block end to include this
            //byte so later we can evaluate blocks
            //of the file.
            blockEnd++;
        }

        //not ended correctly?
        if (currentClass.Parent != null) {
            throw new HotloaderParserException(
                file,
                -1,
                -1,
                "Class not terminated");
        }

        //find all includes that have been removed from the file and remove them
        if (file == null) { return; }
        List<HotloaderFile> oldIncludes = file.Includes;
        foreach (HotloaderFile f in oldIncludes) {
            if (!fileIncludes.Contains(f)) {
                RemoveFile(f);
            }
        }

        //find all variables that have been removed from the file and remove them
        List<HotloaderVariable> oldFiles = file.Variables;
        foreach (HotloaderVariable v in oldFiles) {
            if (!fileVariables.Contains(v)) {
                v.Remove();
            }
        }

        file.setVariables(fileVariables);
        file.setIncludes(fileIncludes);
    }

    private void handleBlock(
                             List<HotloaderVariable> variables,
                             HotloaderFile file, 
                             byte* blockPtr, byte* blockEnd,
                             int currentLine, int currentColumn,
                             ref bool negativeFlag,
                             ref parserMode mode,               
                             ref HotloaderAccessor currentAccessor, 
                             ref HotloaderVariable currentVariable,
                             ref HotloaderExpression currentExpression,
                             ref HotloaderClass currentClass) { 
        
        //read the block as a string
        string block = readString(blockPtr, blockEnd);

        //hash the string so we can do quick compares with keywords
        int hash = block.GetHashCode();

        #region include
        if (hash == STRING_INCLUDE_HASH) { 
            //valid?
            if (mode != parserMode.NONE) {
                throw new HotloaderParserException(
                    file,
                    currentLine,
                    currentColumn,
                    "Unexpected include");
            }
            mode = parserMode.INCLUDE;
            return;
        }
        #endregion

        #region reserve word check
        /*since we detect errors if some keywords
         are used anyway, some are discarded here.*/
        if (mode != parserMode.ASSIGNMENT) {

            if (hash == STRING_TRUE_HASH ||
               hash == STRING_FALSE_HASH) {

                   throw new HotloaderParserException(
                       file,
                       currentLine,
                       currentColumn,
                       String.Format(
                            "Unexpected use of symbol \"{0}\"",
                            block));
            }

        }
        #endregion

        #region accessors

        bool isConst = hash == STRING_CONST_HASH;
        bool isStatic = hash == STRING_STATIC_HASH;

        //valid mode?
        if (mode != parserMode.NONE) {
            if (isConst || isStatic) {
                throw new HotloaderParserException(
                    file,
                    currentLine,
                    currentColumn,
                    "Unexpected accessor");
            }
        }


        if (isConst) { currentAccessor |= HotloaderAccessor.CONST; }
        if (isStatic) { currentAccessor |= HotloaderAccessor.STATIC; }

        if (isConst || isStatic) {
            return;
        }
        #endregion

        #region end of a class?
        if (hash == STRING_END_HASH) { 
            //verify that we can end a class
            if (currentClass.Parent == null ||
                mode != parserMode.NONE) {
                throw new HotloaderParserException(
                    file,
                    currentLine,
                    currentColumn,
                    "Unexpected end of class token.");
            }

            //end the class by just setting
            //the current class to the parent
            currentClass = currentClass.Parent;
            return;
        }
        #endregion

        #region class declaration

        //are we expecting a class name?
        if (mode == parserMode.CLASS) {
            //a class cannot have accessors!
            if (currentAccessor != HotloaderAccessor.NONE) {
                throw new HotloaderParserException(
                    file,
                    currentLine,
                    currentColumn,
                    "Classes cannot have accessors");
            }

            //get the class
            HotloaderClass cls = currentClass.GetClass(block);
            if (cls == null) {
                cls = new HotloaderClass(block, this);

                //only reason this will return false is 
                //if it already exists!
                if (!currentClass.AddClass(cls)) {
                    throw new HotloaderParserException(
                        file,
                        currentLine,
                        currentColumn,
                        String.Format(
                            "Cannot declare class \"{0}\". Name already used elsewhere.",
                            block));
                }
            }
            
            //set the current class to the newly created one
            //so every variable/class added will be added 
            //to this one.
            currentClass = cls;

            //we are no longer reading a class
            mode = parserMode.NONE;
            return;
        }

        #endregion

        #region variable declaration

        //we must be in assignment mode by now..
        if (mode == parserMode.VARIABLE) {
            throw new HotloaderParserException(
                file,
                currentLine,
                currentColumn,
                "Unexpected variable declaration");
        }

        //we assume this is an alpha-numerical string
        if (mode == parserMode.NONE) {
            
            //does this variable already exist?
            //if not, add it.
            currentVariable = currentClass.GetVariable(block);
            if (currentVariable == null) {
                //if the add function returns false, then
                //the variable name is already taken!
                currentVariable = new HotloaderVariable(block, this);
                if (!currentClass.AddVariable(currentVariable)) {
                    throw new HotloaderParserException(
                        file,
                        currentLine,
                        currentColumn,
                        String.Format(
                            "Cannot declare variable \"{0}\". Name already used elsewhere.",
                            block));
                }
            }

            //add to the list of added variables from the file
            variables.Add(currentVariable);

            //if we can't change this variable (it's marked as static
            //just set the current variable to a dummy one.
            if ((currentVariable.Accessors & HotloaderAccessor.STATIC) == HotloaderAccessor.STATIC) {
                currentVariable = new HotloaderVariable("dummy", this);
                currentVariable.changeParent(currentClass);
            }

            //set expression
            currentExpression = currentVariable.Value;
            currentExpression.Clear();

            //set accessors
            currentVariable.Accessors = currentAccessor;
            currentAccessor = HotloaderAccessor.NONE;

            //wait for assignment etc..
            mode = parserMode.VARIABLE;
        }

        #endregion

        #region variable assignment

        if (mode == parserMode.ASSIGNMENT) {
            #region boolean
            string blockLower = block.ToLower();

            if (blockLower == "true" ||
                blockLower == "false") {

                   currentExpression.AddOperand(
                       (block == "true"),
                       HotloaderValueType.BOOLEAN,
                       currentLine,
                       currentColumn,
                       file);
                   return;
            }
            #endregion

            #region numerical?

            double decimalValue;
            bool isDecimal = Double.TryParse(block, out decimalValue);
            if (isDecimal) {
                //is it an actual decimal or integer?
                bool explicitDecimal = block.Contains(".");

                //negate?
                if (negativeFlag) {
                    decimalValue = -decimalValue;
                    negativeFlag = false;
                }

                //deturmine value type
                HotloaderValueType type =
                    explicitDecimal ?
                        HotloaderValueType.DECIMAL :
                        HotloaderValueType.NUMERICAL;

                //deturmine raw object for the string
                object raw = decimalValue;
                if (!explicitDecimal) {
                    raw = (long)decimalValue;
                }

                currentExpression.AddOperand(
                    raw,
                    type,
                    currentLine,
                    currentColumn,
                    file);
                return;
            }

            #endregion

            #region base conversion

            if (block.Length > 2 &&
               *blockPtr == '0') { 
                
                //detect base conversion
                int sourceBase = -1;
                byte sourceBaseByte = *(blockPtr + 1);
                if (sourceBaseByte == 'x') { sourceBase = 16; }
                else if (sourceBaseByte == 'b') { sourceBase = 2; }


                //is it actually a conversion?
                if (sourceBase != -1) { 
                    string convert = block.Substring(2);

                    //attempt to convert
                    try {
                        long converted = Convert.ToInt64(convert, sourceBase);

                        //add the operand
                        currentExpression.AddOperand(
                            converted,
                            HotloaderValueType.NUMERICAL,
                            currentLine,
                            currentColumn,
                            file);
                    }
                    catch {
                        throw new HotloaderParserException(
                            file,
                            currentLine,
                            currentColumn,
                            String.Format(
                                "Unable to convert {0} from base {1}",
                                convert,
                                sourceBase));
                    }
                    return;
                }

            }

            #endregion

            #region variable?

            //verify the name
            while (blockPtr != blockEnd) {
                byte current = *(blockPtr++);
                if (!isNameCharacter(current)) {
                    throw new HotloaderParserException(
                        file,
                        currentLine,
                        currentColumn,
                        String.Format(
                            "Invalid variable name \"{0}\"",
                            block));
                }
            }

            //default to a variable reference
            currentExpression.AddOperand(
                block,
                HotloaderValueType.VARIABLE,
                currentLine,
                currentColumn,
                file);
            #endregion
        }

        #endregion                         
    }

    private bool skipWhitespace(ref byte* ptr, byte* ptrEnd, ref int x, ref int y) {
        while (ptr != ptrEnd) {
            byte current = *ptr;

            
            bool whitespace =
                current == ' ' ||
                current == '\t';
            bool newLine = current == '\n';

            //if we hit a new line, update line number
            if (newLine) {
                ptr++;
                x = 0;
                y++;
                continue;
            }

            //not a whitespace?
            if (!whitespace && !newLine) {
                return false;
            }

            //update line position.
            ptr++;
            x++;
            
        }

        //return true if we are at the end of the string.
        return ptr == ptrEnd;

    }

    private bool isNameCharacter(byte value) {

        return
            /*alpha numeric test*/
            (value >= 'A' &&
            value <= 'Z') ||

            (value >= 'a' &&
            value <= 'z') ||

            (value >= '0' &&
            value <= '9') ||

            /*other name valid characters*/
            value == '_' ||
            value == '.';

    }

    private string readString(byte* start, byte* end) { 
        //empty?
        if (end == start) { return ""; }
        
        //flip?
        if (end < start) {
            byte* temp = end;
            end = start;
            start = temp;
        }

        //
        int length = (int)(end - start);
        return new String((sbyte*)start, 0, (int)(end - start));
    }

    private bool readStringLiteral(ref byte* ptr, byte* end) {        
        //define where we substring from
        while (ptr != end) {
            byte current = *(ptr++);

            //control character? if so, skip over the
            //next character
            if (current == '\\') {
                ptr++;
                continue;
            }

            //we found the end?
            if (current == '"') {
                break;
            }
        }
        
        //only return true if we did 
        //not run at the end of the data
        //meaning a string literal was not found!
        return (ptr != end);
    }

    private readonly int STRING_INCLUDE_HASH =  "include".GetHashCode();
    private readonly int STRING_STATIC_HASH =   "static".GetHashCode();
    private readonly int STRING_CONST_HASH =    "const".GetHashCode();
    private readonly int STRING_END_HASH =      "end".GetHashCode();
    private readonly int STRING_TRUE_HASH =     "true".GetHashCode();
    private readonly int STRING_FALSE_HASH =    "false".GetHashCode();

    [Flags]
    private enum parserMode {
        NONE =       0x00,
        CLASS =      0x01,
        VARIABLE =   0x02,
        NAME =       0x04,
        ASSIGNMENT = 0x08,
        INCLUDE =    0x10
    }
}