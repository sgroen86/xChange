package xchange.validator;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

/**
 * The same rules from the command line, for CI and for checking a file by hand:
 *
 *   java -cp "build/package:build/package/lib/*" xchange.validator.Cli <ruleset-id> file.xml...
 *
 * Prints every failed assertion and exits 1 if any of them is fatal.
 */
public final class Cli {

    private Cli() {
    }

    public static void main(String[] args) throws IOException {
        if (args.length < 2) {
            System.err.println("usage: Cli <ruleset-id> file.xml [file.xml ...]");
            System.exit(2);
        }

        OfficialRules rules = OfficialRules.fromClasspath();
        if (!rules.knows(args[0])) {
            System.err.println("Unknown ruleset " + args[0] + ". Known: " + rules.rulesetIds());
            System.exit(2);
        }

        int fatal = 0;
        for (int i = 1; i < args.length; i++) {
            OfficialRules.Result result = rules.validate(args[0], Files.readString(Path.of(args[i]), StandardCharsets.UTF_8));
            System.out.println("== " + args[i] + " (" + result.rulesetId() + " " + result.release() + ")");
            for (OfficialRules.Failure failure : result.failures()) {
                System.out.println("   " + failure.flag() + "\t" + failure.ruleId() + "\t" + failure.message() + "\t" + failure.location());
                if (failure.isFatal()) {
                    fatal++;
                }
            }
        }

        if (fatal > 0) {
            System.err.println(fatal + " fatal rule violation(s).");
            System.exit(1);
        }
        System.out.println("No fatal rule violations.");
    }
}
