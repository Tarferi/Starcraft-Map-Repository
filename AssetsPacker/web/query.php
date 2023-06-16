<?php

function gn($name) {
	return isset ( $_GET [$name] ) ? $_GET [$name] : (isset ( $_POST [$name] ) ? $_POST [$name] : null);
}

$version = gn ( "version" );
$action = gn ( "action" );

function runV1_0() {
	die ( "[]" );
}

class Config {

	private function __construct($handle) {
		$this->handle = $handle;
		$buffer = [ ];
		while ( true ) {
			$r = fread ( $this->handle, 1024 );
			if ($r === false || $r === "") {
				break;
			}
			$buffer [] = $r;
		}
		$buffer = implode ( "", $buffer );
		if ($buffer == "") {
			$buffer = "{}";
			$this->data = array ();
		} else {
			$this->data = json_decode ( $buffer, true );
		}
		if ($this->data === null) {
			$this->Release ();
			die ( "Config format error" );
		}
	}

	public static function load() {
		$filename = "config.jsn";
		if (! file_exists ( $filename )) {
			file_put_contents ( $filename, "{}" );
		}
		$fp = fopen ( $filename, 'r+' );
		if (flock ( $fp, LOCK_EX )) {
			return new Config ( $fp );
		} else {
			return null;
		}
	}

	public function Release() {
		$d = json_encode ( $this->data );
		$er = false;
		fseek ( $this->handle, 0, SEEK_SET );
		if (fwrite ( $this->handle, $d ) === false) {
			$er = true;
		}
		flock ( $this->handle, LOCK_UN );
		fclose ( $this->handle );
		if ($er) {
			die ( "Failed to save config file" );
		}
	}

	public function Get($key, $defaultValue) {
		if (isset ( $this->data [$key] )) {
			return $this->data [$key];
		} else {
			return $defaultValue;
		}
	}

	public function Set($key, $value) {
		$this->data [$key] = $value;
	}

	public function UpdateFile($file, $name, $type, $size, $parts) {
		$files = $this->Get ( "files", array () );
		foreach ( $files as $cfile ) {
			$cfgname = $cfile ["file"];
			$cftype = $cfile ["type"];
			if ($cfgname == $file && $cftype == $type) {
				$cfile ["name"] = $name;
				$cfile ["size"] = $size;
				$cfile ["parts"] = $parts;
				$this->Set ( "files", $files );
				return;
			}
		}

		$cfile = array ();
		$cfile ["file"] = $file;
		$cfile ["name"] = $name;
		$cfile ["type"] = $type;
		$cfile ["size"] = $size;
		$cfile ["parts"] = $parts;
		$files [] = $cfile;
		$this->Set ( "files", $files );
	}
}

function PUT_FILE($file, $name, $type, $size, $parts, $part, $data, $cfg) {
	$cfg->UpdateFile ( $file, $name, $type, $size, $parts );
	$filename = $type . "_" . $file . "_" . $part;
	file_put_contents ( $filename, $data );
}

function GET_FILES() {
	header ( 'Location: config.jsn' );
	die ();
}

function runV1_1() {
	global $action;
	if ($action == "PUT") {
		$key = gn ( "key" );
		include "key.php";
		if ($key != KEY) {
			die ( "Invalid key" );
		}
		$file = gn ( "file" );
		$name = gn ( "name" );
		$size = gn ( "size" );
		$type = gn ( "type" );
		$parts = gn ( "parts" );
		$part = gn ( "part" );
		$data = gn ( "data" );
		if ($file === null || $size === null || $name === null || $parts === null || $parts === null || $data === null) {
			die ( "Missing arguments" );
		}
		$size *= 1;
		$parts *= 1;
		$part *= 1;
		$type *= 1;
		$data = base64_decode ( $data );
		$cfg = Config::load ();
		if ($cfg == null) {
			die ( "Failed to open config file" );
		} else {
			try {
				PUT_FILE ( $file, $name, $type, $size, $parts, $part, $data, $cfg );
			} finally {
				$cfg->Release ();
			}
			die ( "OK" );
		}
	} else if ($action == "GET") {

		$cfg = Config::load ();
		if ($cfg == null) {
			die ( "Failed to open config file" );
		} else {
			try {
				GET_FILES ( $cfg );
			} finally {
				$cfg->Release ();
			}
		}
	} else {
	}
}
if ($version == "1") {
	runV1_0 ();
} else if ($version == "1.1") {
	runV1_1 ();
}
/*
 * [ { "name": "Remastered tileset", "size": 9833691, "type": 13, "file":
 * "Remaster.bin" }, { "name": "Carbot tileset", "size": 36744526, "type":
 * 13, "file": "Carbot.bin" }, { "name": "HD tileset", "size": 197890477,
 * "type": 13, "file": "HD.bin" } ]
 */
?>
